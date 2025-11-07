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
    /// <summary>
    /// Upload file theo từng chunk (phần nhỏ), hỗ trợ nhiều định dạng
    /// </summary>
    public class UploadCommand
    {
        private const long MaxChunkSize = 500 * 1024;      // 500KB mỗi chunk
        private const long MaxTotalFileSize = 10 * 1024 * 1024; // 5MB tổng

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx"
        };

        private readonly AppDBContext _db;
        private readonly IHostEnvironment _env;

        public UploadCommand(AppDBContext db, IHostEnvironment env)
        {
            _db = db;
            _env = env;
            Directory.CreateDirectory(WebRootPath);
            Directory.CreateDirectory(TempUploadFolder); // Tạo folder temp để lưu chunks
        }

        /// <summary>
        /// Upload 1 chunk → nếu là chunk cuối thì tự động ghép file
        /// </summary>
        public async Task<UploadChunkResult> UploadChunkAsync(ChunkRequest meta, Stream chunkStream)
        {
            ValidateMeta(meta);
            ValidateStream(chunkStream);

            var uploadId = GetOrCreateUploadId(meta);

            // Lưu chunk tạm
            await SaveChunkToTemp(uploadId, meta.ChunkIndex, chunkStream);

            // Nếu là chunk cuối → ghép file
            if (meta.ChunkIndex == meta.TotalChunks - 1)
            {
                var file = await MergeAllChunks(uploadId, meta.OriginalFileName, meta.TotalChunks, meta.TotalSizeBytes);
                return new UploadChunkResult { Completed = true, UploadId = uploadId, File = file };
            }

            return new UploadChunkResult { Completed = false, UploadId = uploadId };
        }

        #region Private Helpers

        private static void ValidateMeta(ChunkRequest meta)
        {
            if (meta == null) throw new ArgumentNullException(nameof(meta));
            if (string.IsNullOrWhiteSpace(meta.OriginalFileName)) throw new ArgumentException("Tên file không được để trống");
            if (meta.ChunkIndex < 0) throw new ArgumentOutOfRangeException(nameof(meta.ChunkIndex));
            if (meta.TotalChunks <= 0) throw new ArgumentOutOfRangeException(nameof(meta.TotalChunks));
            if (meta.ChunkIndex >= meta.TotalChunks) throw new ArgumentException("ChunkIndex phải nhỏ hơn TotalChunks");
        }

        private static void ValidateStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
        }

        private static string GetOrCreateUploadId(ChunkRequest meta)
        {
            return string.IsNullOrWhiteSpace(meta.UploadId)
                ? Guid.NewGuid().ToString("N")
                : meta.UploadId.Trim().Replace("\"", "");
        }

        private async Task SaveChunkToTemp(string uploadId, int chunkIndex, Stream chunkStream)
        {
            var tempPath = Path.Combine(TempUploadFolder, $"{uploadId}_{chunkIndex}.chunk");

            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await chunkStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                totalRead += bytesRead;
                if (totalRead > MaxChunkSize)
                {
                    fileStream.Dispose();
                    TryDeleteFile(tempPath);
                    throw new InvalidOperationException($"Chunk vượt quá {MaxChunkSize / 1024}KB");
                }
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            }
        }

        private async Task<UploadModel> MergeAllChunks(string uploadId, string originalName, int totalChunks, long? expectedTotalSize)
        {
            var safeName = Path.GetFileName(originalName);
            var ext = Path.GetExtension(safeName).ToLowerInvariant();
            ThrowIfExtensionNotAllowed(ext);

            var (totalSize, actualChunkCount) = await ValidateAllChunks(uploadId, totalChunks);
            ThrowIfSizeExceeded(totalSize, expectedTotalSize);

            var publicPath = await SaveMergedFile(uploadId, actualChunkCount, safeName, ext);
            var fileInfo = new FileInfo(GetPhysicalPath(publicPath));

            return new UploadModel
            {
                FileName = safeName,
                FilePath = publicPath,
                FileExtension = ext,
                FileSizeKB = (int)Math.Ceiling(fileInfo.Length / 1024.0),
                Status = 1,
                CreateAt = DateTime.UtcNow,
                CreateBy = 0
            };
        }

        private async Task<(long totalSize, int chunkCount)> ValidateAllChunks(string uploadId, int expectedChunks)
        {
            long total = 0;
            int count = 0;

            while (true)
            {
                if (expectedChunks > 0 && count >= expectedChunks) break;

                var chunkPath = Path.Combine(TempUploadFolder, $"{uploadId}_{count}.chunk");
                if (!File.Exists(chunkPath))
                {
                    if (expectedChunks > 0)
                        throw new FileNotFoundException($"Thiếu chunk: {count}");
                    break;
                }

                var size = new FileInfo(chunkPath).Length;
                total += size;
                if (total > MaxTotalFileSize)
                    throw new InvalidOperationException($"Tổng dung lượng vượt quá {MaxTotalFileSize / 1024 / 1024}MB");

                count++;
            }

            if (expectedChunks > 0 && count != expectedChunks)
                throw new InvalidOperationException($"Số chunk thực tế ({count}) không khớp với yêu cầu ({expectedChunks})");

            return (total, count);
        }

        private static void ThrowIfSizeExceeded(long actualSize, long? expectedSize)
        {
            if (expectedSize.HasValue && actualSize > expectedSize.Value)
                throw new InvalidOperationException($"Dung lượng thực tế ({actualSize}) vượt quá khai báo ({expectedSize})");
        }

        private static void ThrowIfExtensionNotAllowed(string ext)
        {
            if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            {
                var list = string.Join(", ", AllowedExtensions.OrderBy(e => e));
                throw new ArgumentException($"Chỉ hỗ trợ: {list}", nameof(ext));
            }
        }

        private async Task<string> SaveMergedFile(string uploadId, int chunkCount, string fileName, string ext)
        {
            var publicFolder = CreateDateFolder("/media/upload");

            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var sanitized = SanitizeFileName(baseName);
            if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "file";

            // Include uploadId to avoid collisions even with same original names
            var finalFileName = $"{sanitized}_{uploadId}{ext}";
            var publicFilePath = $"{publicFolder}/{finalFileName}";
            var physicalPath = GetPhysicalPath(publicFilePath);

            Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

            using var output = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            for (int i = 0; i < chunkCount; i++)
            {
                var chunkPath = Path.Combine(TempUploadFolder, $"{uploadId}_{i}.chunk");
                using var input = new FileStream(chunkPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                await input.CopyToAsync(output);
                TryDeleteFile(chunkPath);
            }

            return publicFilePath;
        }

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

            foreach (var part in new[] { now.Year.ToString(), now.Month.ToString("D2"), now.Day.ToString("D2") })
            {
                path += $"/{part}";
                var fullPath = GetPhysicalPath(path);
                if (!Directory.Exists(fullPath))
                    Directory.CreateDirectory(fullPath);
            }

            return path;
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch {  }
        }

        #endregion

        #region Path Helpers

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

