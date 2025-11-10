using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DBContext.Reportly;
using DBContext.Reportly.Entities;
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
            ".jpg", ".jpeg", ".png", ".pdf"
        };

        private readonly AppDBContext _db;
        private readonly IHostEnvironment _env;

        public UploadCommand(AppDBContext db, IHostEnvironment env)
        {
            _db = db;
            _env = env;
            // Tạo thư mục nếu chưa có
            if (!Directory.Exists(WebRootPath)) Directory.CreateDirectory(WebRootPath);
            if (!Directory.Exists(TempUploadFolder)) Directory.CreateDirectory(TempUploadFolder);
        }

        public async Task<UploadChunkResult> UploadChunkAsync(ChunkRequest meta, Stream chunkStream)
        {
            // Validate & Get ID
            (string uploadId, string fileExt) ValidateRequestAndGetId()
            {
                if (meta == null) throw new ArgumentNullException(nameof(meta));
                if (string.IsNullOrWhiteSpace(meta.OriginalFileName)) throw new ArgumentException("Tên file không được để trống");
                if (meta.ChunkIndex < 0 || meta.TotalChunks <= 0 || meta.ChunkIndex >= meta.TotalChunks)
                     throw new ArgumentOutOfRangeException("ChunkIndex hoặc TotalChunks không hợp lệ");

                var ext = Path.GetExtension(meta.OriginalFileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
                {
                    throw new ArgumentException($"Định dạng file không được hỗ trợ: {ext}");
                }

                var id = string.IsNullOrWhiteSpace(meta.UploadId) ? Guid.NewGuid().ToString("N") : meta.UploadId.Trim();
                return (id, ext);
            }

            //Merge
            async Task<UploadModel> MergeAllChunks(string uploadId, string originalName, int expectedChunks, long? expectedTotalSize, string ext)
            {
                long totalSize = 0;
                // Validate đủ số lượng chunk trước khi ghép
                for (int i = 0; i < expectedChunks; i++)
                {
                    var chunkPath = Path.Combine(TempUploadFolder, $"{uploadId}_{i}.chunk");
                    if (!File.Exists(chunkPath)) throw new FileNotFoundException($"Thiếu chunk thứ {i}");
                    totalSize += new FileInfo(chunkPath).Length;
                }

                if (totalSize > MaxTotalFileSize) throw new InvalidOperationException("Tổng dung lượng file vượt quá giới hạn cho phép");
                if (expectedTotalSize.HasValue && totalSize > expectedTotalSize.Value) throw new InvalidOperationException("Dung lượng thực tế không khớp với khai báo ban đầu");

                // Bắt đầu ghép
                var safeName = Path.GetFileName(originalName);
                var relativeFolder = CreateDateFolder("/media/upload");
                var finalFileName = $"{Path.GetFileNameWithoutExtension(safeName)}_{uploadId}{ext}";
                var publicFilePath = $"{relativeFolder}/{finalFileName}";
                var physicalPath = GetPhysicalPath(publicFilePath);

                using (var output = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    for (int i = 0; i < expectedChunks; i++)
                    {
                        var chunkPath = Path.Combine(TempUploadFolder, $"{uploadId}_{i}.chunk");
                        using var input = File.OpenRead(chunkPath);
                        await input.CopyToAsync(output);
                        try { File.Delete(chunkPath); } catch {  }
                    }
                }

                return new UploadModel
                {
                    FileName = safeName,
                    FilePath = publicFilePath, 
                    FileExtension = ext,
                    FileSizeKB = (int)(totalSize / 1024),
                    Status = 1,
                    CreatedAt = DateTime.Now,
                    CreatedBy = 0 
                };
            }

            // --- Main Execution Flow ---
            var (uId, fExt) = ValidateRequestAndGetId();

            // Lưu chunk hiện tại
            var currentChunkPath = Path.Combine(TempUploadFolder, $"{uId}_{meta.ChunkIndex}.chunk");
            using (var fs = new FileStream(currentChunkPath, FileMode.Create))
            {
                await chunkStream.CopyToAsync(fs);
            }
            
            // Kiểm tra kích thước chunk vừa upload
            if (new FileInfo(currentChunkPath).Length > MaxChunkSize)
            {
                File.Delete(currentChunkPath);
                throw new InvalidOperationException($"Chunk vượt quá giới hạn {MaxChunkSize / 1024}KB");
            }

            // Nếu là chunk cuối thì tiến hành ghép
            if (meta.ChunkIndex == meta.TotalChunks - 1)
            {
                var fileModel = await MergeAllChunks(uId, meta.OriginalFileName, meta.TotalChunks, meta.TotalSizeBytes, fExt);

                
                var entity = new Upload
                {
                    FileName = fileModel.FileName,
                    FilePath = fileModel.FilePath,
                    FileExtension = fileModel.FileExtension,
                    FileSizeKB = fileModel.FileSizeKB,
                    Status = fileModel.Status,
                    CreatedAt = fileModel.CreatedAt,
                    CreatedBy = fileModel.CreatedBy
                };
                _db.Uploads.Add(entity);
                await _db.SaveChangesAsync();
                
                fileModel.Id = entity.Id;
                return new UploadChunkResult { Completed = true, UploadId = uId, File = fileModel };
            }

            return new UploadChunkResult { Completed = false, UploadId = uId };
        }

        // Helper
        private string WebRootPath => Path.Combine(_env.ContentRootPath, "wwwroot");
        private string TempUploadFolder => Path.Combine(WebRootPath, "content", "temp_upload");
        
        private string GetPhysicalPath(string publicPath)
        {
             return Path.Combine(WebRootPath, publicPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        }

        private string CreateDateFolder(string basePath)
        {
            var now = DateTime.Now;
            var relative = $"{now.Year}/{now.Month:D2}/{now.Day:D2}";
            var finalPath = $"{basePath.TrimEnd('/')}/{relative}";
            Directory.CreateDirectory(GetPhysicalPath(finalPath));
            return finalPath;
        }
    }
}