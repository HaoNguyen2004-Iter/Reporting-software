using Microsoft.AspNetCore.Mvc;
using Service.Reportly.Executes.Emails;
using Service.Reportly.Executes.Uploads;
using Service.Reportly.Model;

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

        // API Upload
        [HttpPost("upload-chunk")]
        public async Task<IActionResult> UploadChunk([FromForm] ChunkRequest request, IFormFile chunk)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return Unauthorized(new { message = "Hết phiên đăng nhập." });

            try
            {
                if (chunk == null || chunk.Length == 0) return BadRequest(new { message = "Chunk trống." });

                using var stream = chunk.OpenReadStream();
                var result = await _uploadCommand.UploadChunkAsync(request, stream);

                if (result.Completed)
                {
                    // Nếu completed, file đã được lưu vào DB với CreatedBy = 0 (trong UploadCommand cũ).
                    // Cần update lại CreatedBy đúng của user nếu muốn kỹ hơn.
                    return Ok(new { completed = true, uploadId = result.UploadId, file = result.File });
                }

                return Ok(new { completed = false, uploadId = result.UploadId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chunk upload error");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendEmail([FromForm] EmailModels model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return Unauthorized(new { message = "Hết phiên đăng nhập." });

            // Lấy thông tin người gửi từ Session để đảm bảo chính xác
            model.CreatedBy = userId.Value;
            model.SenderName = HttpContext.Session.GetString("FullName");
            model.SenderDepartment = HttpContext.Session.GetString("DepartmentName");

            try
            {
                // Đã có UploadId 
                if (model.UploadId.HasValue && model.UploadId.Value > 0)
                {
                   // Chưa làm
                }
                // Có FilePath
                else
                {
                     return BadRequest(new { message = "Vui lòng đính kèm file báo cáo (UploadId missing)." });
                }

                var success = await _emailCommand.SendAsync(model);
                return success ? Ok(new { message = "Gửi email thành công!" })
                               : StatusCode(500, new { message = "Gửi email thất bại." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Send email error");
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}