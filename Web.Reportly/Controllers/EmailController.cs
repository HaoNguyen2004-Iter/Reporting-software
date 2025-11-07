using Microsoft.AspNetCore.Mvc;
using Service.Reportly.Executes.Emails;
using Service.Reportly.Executes.Uploads;
using Service.Reportly.Model;
using System.Threading.Tasks;

namespace Reportly.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly EmailCommand _emailCommand;
        private readonly UploadCommand _uploadCommand;
        private readonly ILogger<EmailController> _logger;

        public EmailController(EmailCommand emailCommand, UploadCommand uploadCommand, ILogger<EmailController> logger)
        {
            _emailCommand = emailCommand;
            _uploadCommand = uploadCommand;
            _logger = logger;
        }

        [HttpPost("upload-chunk")]
        public async Task<IActionResult> UploadChunk([FromForm] string uploadId, 
            [FromForm] string originalFileName,
            [FromForm] int chunkIndex,
            [FromForm] int totalChunks,
            [FromForm] long totalSizeBytes,
            IFormFile chunk)
        {
            //  TÀI KHOẢN (UserId) ===
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue || userId.Value == 0)
            {
                return Unauthorized(new { message = "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại." });
            }
            // ======================================

            try
            {
                if (chunk == null || chunk.Length == 0)
                    return BadRequest(new { message = "Chunk không hợp lệ!" });

                var chunkRequest = new ChunkRequest
                {
                    UploadId = uploadId,
                    OriginalFileName = originalFileName,
                    ChunkIndex = chunkIndex,
                    TotalChunks = totalChunks,
                    TotalSizeBytes = totalSizeBytes
                };

                using var stream = chunk.OpenReadStream();
                var result = await _uploadCommand.UploadChunkAsync(chunkRequest, stream);

                if (result.Completed)
                {
                    _logger.LogInformation("Upload completed for UserId {UserId}", userId);
                    return Ok(new 
                    { 
                        completed = true,
                        uploadId = result.UploadId,
                        file = result.File, // Trả về toàn bộ object file
                        message = "Upload hoàn tất!"
                    });
                }

                return Ok(new 
                { 
                    completed = false,
                    uploadId = result.UploadId,
                    message = $"Đã nhận chunk {chunkIndex + 1}/{totalChunks}"
                });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error for UserId {UserId}: {Message}", userId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading chunk for UserId {UserId}: {Message}", userId, ex.Message);
                return StatusCode(500, new { message = $"Lỗi upload chunk: {ex.Message}" });
            }
        }


        [HttpPost("send")]
        public async Task<IActionResult> SendEmail([FromForm] EmailModels model)
        {
           
            //Kiểm tra UserId từ Session
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue || userId.Value == 0)
            {
                return Unauthorized(new { message = "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại." });
            }
            
            // Ghi đè CreatedBy bằng ID (int) từ Session (Bảo mật)
            model.CreatedBy = userId.Value;

         
            model.SenderName = HttpContext.Session.GetString("FullName") ?? "Không rõ";
            model.SenderDepartment = HttpContext.Session.GetString("DepartmentName") ?? "Không rõ";
            
    

            try
            {
                if (string.IsNullOrWhiteSpace(model.FilePath))
                    return BadRequest(new { message = "Đường dẫn file không được để trống!" });

                // Chuyển public path -> physical path
                var publicPath = model.FilePath; // Lưu lại đường dẫn public
                
                var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", model.FilePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar));

                if (!System.IO.File.Exists(physicalPath))
                {
                    _logger.LogWarning("File not found at physical path: {Path}", physicalPath);
                    return BadRequest(new { message = "File không tồn tại!" });
                }

                model.FilePath = physicalPath; // Gán Physical path cho EmailCommand

                // Truyền publicPath (để lưu vào DB)
                var success = await _emailCommand.SendAsync(model, publicPath); 

                return success
                    ? Ok(new { message = "Gửi email thành công!" })
                    : StatusCode(500, new { message = "Lỗi khi gửi mail!" });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Validation error for UserId {UserId}: {Message}", userId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email for UserId {UserId}: {Message}", userId, ex.Message);
                var errorMessage = ex.InnerException != null 
                    ? $"{ex.Message} | Chi tiết: {ex.InnerException.Message}"
                    : ex.Message;
                return StatusCode(500, new { message = $"Lỗi: {errorMessage}" });
            }
        }
    }
}