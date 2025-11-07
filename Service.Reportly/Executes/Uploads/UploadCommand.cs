using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DBContext.Reportly;
using Microsoft.Extensions.Hosting;
using Service.Reportly.Model;

namespace Service.Reportly.Executes.Uploads
{
    public class UploadCommand
    {
        private const long MaxChunkSize = 500 * 1024;
        private const long MaxTotalFileSize = 10 * 1024 * 1024;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png",
            ".pdf"
        };

        private readonly AppDBContext _db;
        private readonly IHostEnvironment _env;

        public UploadCommand(AppDBContext db, IHostEnvironment env)
        {
            _db = db;
            _env = env;
            Directory.CreateDirectory(WebRootPath);
            Directory.CreateDirectory(TempUploadFolder);
        }

        /// <summary>
        /// Upload 1 chunk → nếu là chunk cuối thì tự động ghép file.
        /// Toàn bộ logic luồng chính được tích hợp tại đây bằng Local Functions.
        /// </summary>
        public async Task<UploadChunkResult> UploadChunkAsync(ChunkRequest meta, Stream chunkStream)
        {
           
            // 1. Gộp ValidateMeta, ValidateStream và GetOrCreateUploadId
            (string uploadId, string fileExt) ValidateRequestAndGetId()
            {
                if (meta == null) throw new ArgumentNullException(nameof(meta));
                if (chunkStream == null) throw new ArgumentNullException(nameof(chunkStream));
                if (string.IsNullOrWhiteSpace(meta.OriginalFileName)) throw new ArgumentException("Tên file không được để trống");
                if (meta.ChunkIndex < 0) throw new ArgumentOutOfRangeException(nameof(meta.ChunkIndex));
                if (meta.TotalChunks <= 0) throw new ArgumentOutOfRangeException(nameof(meta.TotalChunks));
                if (meta.ChunkIndex >= meta.TotalChunks) throw new ArgumentException("ChunkIndex phải nhỏ hơn TotalChunks");

                var safeName = Path.GetFileName(meta.OriginalFileName);
                var ext = Path.GetExtension(safeName).ToLowerInvariant();

                // Kiểm tra Extension 
                if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
                {
                    var list = string.Join(", ", AllowedExtensions.OrderBy(e => e));
                    throw new ArgumentException($"Chỉ hỗ trợ: {list}", nameof(ext));
                }

                // Tạo mã cho chunk
                var id = string.IsNullOrWhiteSpace(meta.UploadId)
                    ? Guid.NewGuid().ToString("N")
                    : meta.UploadId.Trim().Replace("\"", "");

                return (id, ext);
            }

            // 2. Gộp SaveChunkToTemp
            async Task SaveChunk(string uploadId, int chunkIndex)
            {
                var tempPath = Path.Combine(TempUploadFolder, $"{uploadId}_{chunkIndex}.chunk");
                long totalRead = 0;

                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    int bytesRead;

                    while ((bytesRead = await chunkStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                    {
                        totalRead += bytesRead;
                        if (totalRead > MaxChunkSize)
                        {
                            TryDeleteFile(tempPath);
                            throw new InvalidOperationException($"Chunk vượt quá {MaxChunkSize / 1024}KB");
                        }
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    }
                }
            }

            // 3. Gộp MergeAllChunks và ValidateAllChunks
            async Task<UploadModel> MergeAllChunks(string uploadId, string originalName, int expectedChunks, long? expectedTotalSize, string ext)
            {
                // Logic Validate All Chunks
                long total = 0;
                int actualChunkCount = 0;
                for (actualChunkCount = 0; ; actualChunkCount++)
                {
                    if (expectedChunks > 0 && actualChunkCount >= expectedChunks) break;
                    var chunkPath = Path.Combine(TempUploadFolder, $"{uploadId}_{actualChunkCount}.chunk");

                    if (!File.Exists(chunkPath))
                    {
                        if (expectedChunks > 0) throw new FileNotFoundException($"Thiếu chunk: {actualChunkCount}");
                        break;
                    }

                    var size = new FileInfo(chunkPath).Length;
                    total += size;
                    if (total > MaxTotalFileSize)
                        throw new InvalidOperationException($"Tổng dung lượng vượt quá {MaxTotalFileSize / 1024 / 1024}MB");
                }

                if (expectedChunks > 0 && actualChunkCount != expectedChunks)
                    throw new InvalidOperationException($"Số chunk thực tế ({actualChunkCount}) không khớp với yêu cầu ({expectedChunks})");

                // Kiểm tra tổng kích thước 
                if (expectedTotalSize.HasValue && total > expectedTotalSize.Value)
                    throw new InvalidOperationException($"Dung lượng thực tế ({total}) vượt quá khai báo ({expectedTotalSize})");

                // Logic Save Merged File 
                var safeName = Path.GetFileName(originalName);
                var publicFolder = CreateDateFolder("/media/upload");
                var baseName = Path.GetFileNameWithoutExtension(safeName);
                var sanitized = SanitizeFileName(baseName);
                if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "file";

                var finalFileName = $"{sanitized}_{uploadId}{ext}";
                var publicFilePath = $"{publicFolder}/{finalFileName}";
                var physicalPath = GetPhysicalPath(publicFilePath);

                Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

                using (var output = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    for (int i = 0; i < actualChunkCount; i++)
                    {
                        var chunkPath = Path.Combine(TempUploadFolder, $"{uploadId}_{i}.chunk");
                        using var input = File.OpenRead(chunkPath);
                        await input.CopyToAsync(output);
                        TryDeleteFile(chunkPath); // Xóa chunk sau khi ghép
                    }
                }

                var fileInfo = new FileInfo(GetPhysicalPath(publicFilePath));

                return new UploadModel
                {
                    FileName = safeName,
                    FilePath = publicFilePath,
                    FileExtension = ext,
                    FileSizeKB = (int)Math.Ceiling(fileInfo.Length / 1024.0),
                    Status = 1,
                    CreateAt = DateTime.UtcNow,
                    CreateBy = 0
                };
            }

            var (uploadId, fileExt) = ValidateRequestAndGetId();
            await SaveChunk(uploadId, meta.ChunkIndex);

            if (meta.ChunkIndex == meta.TotalChunks - 1)
            {
                var file = await MergeAllChunks(uploadId, meta.OriginalFileName, meta.TotalChunks, meta.TotalSizeBytes, fileExt);
                return new UploadChunkResult { Completed = true, UploadId = uploadId, File = file };
            }

            return new UploadChunkResult { Completed = false, UploadId = uploadId };
        }

        #region Helper

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Where(ch => !invalid.Contains(ch)).ToArray());
            cleaned = string.Join("_", cleaned.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            return cleaned.Length > 64 ? cleaned[..64] : cleaned;
        }

        private string CreateDateFolder(string basePath)
        {
            var now = DateTime.Now;
            var path = basePath.TrimEnd('/');

            var relativePath = Path.Combine(now.Year.ToString(), now.Month.ToString("D2"), now.Day.ToString("D2"));
            var finalPublicPath = $"{path}/{relativePath.Replace(Path.DirectorySeparatorChar, '/')}";
            var fullPath = GetPhysicalPath(finalPublicPath);

            Directory.CreateDirectory(fullPath);

            return finalPublicPath;
        }

        private static void TryDeleteFile(string path)
        {
            try { File.Delete(path); }
            catch { }
        }

        private string WebRootPath => Path.Combine(_env.ContentRootPath ?? AppContext.BaseDirectory, "wwwroot");

        private string TempUploadFolder => Path.Combine(WebRootPath, "content", "temp_upload");

        private string GetPhysicalPath(string publicPath)
        {
            if (string.IsNullOrEmpty(publicPath)) return WebRootPath;
            var clean = publicPath.TrimStart('/');
            return Path.Combine(WebRootPath, clean.Replace('/', Path.DirectorySeparatorChar));
        }

        #endregion
    }
}