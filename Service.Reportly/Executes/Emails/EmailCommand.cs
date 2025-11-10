using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DBContext.Reportly;
using DBContext.Reportly.Entities;
using FluentEmail.Core;
using FluentEmail.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace Service.Reportly.Executes.Emails
{
    public class EmailCommand
    {
        private readonly AppDBContext _context;
        private readonly IFluentEmail _fluentEmail;
        private readonly IHostEnvironment _env; 

        public EmailCommand(AppDBContext context, IFluentEmail fluentEmail, IHostEnvironment env)
        {
            _context = context;
            _fluentEmail = fluentEmail;
            _env = env;
        }

        public async Task<bool> SendAsync(EmailModels model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (SqlGuard.IsSuspicious(model)) throw new InvalidOperationException("Phát hiện dữ liệu đầu vào không an toàn.");

            // 1. Tạo Log trước (trạng thái Pending = 0)
          
            var emailLog = new DBContext.Reportly.Entities.Email
            {
                ToEmail = model.ToEmail,
                CCEmail = model.CCEmail,
                Subject = model.Subject,
                Content = model.Content,
                Status = 0, 
                UploadId = model.UploadId, // Chỉ lưu UploadId làm tham chiếu
                CreatedAt = DateTime.Now,
                CreatedBy = model.CreatedBy,
                SenderName = model.SenderName,
                SenderDepartment = model.SenderDepartment
            };

            _context.EmailLogs.Add(emailLog);
            await _context.SaveChangesAsync();

            try
            {
                var emailBuilder = _fluentEmail
                    .To(model.ToEmail)
                    .Subject(model.Subject)
                    .Body(string.IsNullOrWhiteSpace(model.Content) ? "<p>...</p>" : model.Content, true);

                if (!string.IsNullOrWhiteSpace(model.CCEmail))
                {
                    foreach (var cc in model.CCEmail.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        emailBuilder.CC(cc.Trim());
                    }
                }

                // 2. Xử lý file đính kèm dựa trên UploadId (Single Source of Truth)
                if (model.UploadId.HasValue)
                {
                    var upload = await _context.Uploads.FindAsync(model.UploadId.Value);
                    if (upload != null)
                    {
                        // Convert Public Path (DB) -> Physical Path (Server)
                        var physicalPath = GetPhysicalPath(upload.FilePath);
                        
                        if (File.Exists(physicalPath))
                        {
                            var fileBytes = await File.ReadAllBytesAsync(physicalPath);
                            emailBuilder.Attach(new Attachment
                            {
                                Data = new MemoryStream(fileBytes),
                                Filename = upload.FileName, // Lấy tên gốc từ DB Uploads
                                ContentType = GetContentType(upload.FileExtension) // Hàm helper xác định loại file
                            });
                        }
                        else
                        {
                            // File trong DB có nhưng ổ cứng không thấy -> Log cảnh báo
                            emailLog.Content += $"\n[Warning]: Không tìm thấy file vật lý tại {physicalPath}";
                        }
                    }
                }

                // 3. Gửi và cập nhật trạng thái
                var response = await emailBuilder.SendAsync();
                emailLog.Status = response.Successful ? 1 : 0; // 1: Success, 0: Failed
                
                if (!response.Successful)
                {
                    emailLog.Content += $"\n[Error]: {string.Join("; ", response.ErrorMessages)}";
                }

                await _context.SaveChangesAsync();
                return response.Successful;
            }
            catch (Exception ex)
            {
                emailLog.Status = 2; // Failed
                emailLog.Content += $"\n[Exception]: {ex.Message}";
                await _context.SaveChangesAsync();
                throw; // Ném tiếp lỗi để Controller biết
            }
        }

        // Helpers
        private string GetPhysicalPath(string publicPath)
        {
            var webRoot = Path.Combine(_env.ContentRootPath, "wwwroot");
            return Path.Combine(webRoot, publicPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        }

        private string GetContentType(string ext)
        {
            return ext?.ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" or ".xlsx" => "application/vnd.ms-excel",
                _ => "application/octet-stream"
            };
        }
    }
}