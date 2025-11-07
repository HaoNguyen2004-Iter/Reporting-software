using DBContext.Reportly;
using DBContext.Reportly.Entities;
using FluentEmail.Core;
using FluentEmail.Core.Models;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Service.Reportly.Executes.Emails
{
    public class EmailCommand
    {
        private readonly AppDBContext _context;
        private readonly IFluentEmail _fluentEmail;

        public EmailCommand(AppDBContext context, IFluentEmail fluentEmail)
        {
            _context = context;
            _fluentEmail = fluentEmail;
        }

        public async Task<bool> SendAsync(EmailModels model)
        {
            if (model == null) throw new ArgumentNullException("Email không hợp lệ");

            if (SqlGuard.IsSuspicious(model))
                throw new Exception("Đầu vào không hợp lệ");
            // Tạo email log trước, trạng thái mặc định = 0 (chưa gửi/thất bại)
            var emailLog = new DBContext.Reportly.Entities.Email
            {
                ToEmail = model.ToEmail,
                CCEmail = model.CCEmail,
                Subject = model.Subject,
                Content = model.Content,
                Status = 0,
                FilePath = model.PublicFilePath, // lưu đường dẫn public (nếu có)
                OriginalFileName = model.OriginalFileName,
                CreatedAt = DateTime.Now,
                CreatedBy = model.CreatedBy,
                UploadId = model.UploadId,
                SenderName = model.SenderName,
                SenderDepartment = model.SenderDepartment
            };
            _context.EmailLogs.Add(emailLog);
            await _context.SaveChangesAsync();

            // Gửi email và cập nhật trạng thái (không rollback bản ghi log)
            try
            {
                if (string.IsNullOrWhiteSpace(model.FilePath))
                     throw new ArgumentException("Physical FilePath is null or empty");

                var content = string.IsNullOrWhiteSpace(model.Content) ? "<p>Nội dung trống</p>" : model.Content;
                
                var email = _fluentEmail
                    .To(model.ToEmail)
                    .Subject(model.Subject)
                    .Body(content, true);

                if (!string.IsNullOrWhiteSpace(model.CCEmail))
                {
                    var ccEmails = model.CCEmail
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(e => e.Trim())
                        .Where(e => !string.IsNullOrWhiteSpace(e))
                        .ToArray();
                    
                    if (ccEmails.Length > 0)
                    {
                        
                        foreach (var ccEmail in ccEmails)
                        {
                            email.CC(ccEmail);
                        }
                    }
                }

                // Đọc file từ Physical Path
                var fileBytes = await System.IO.File.ReadAllBytesAsync(model.FilePath);
                // Đặt tên file đính kèm là Tên file gốc
                var fileNameForAttachment = model.OriginalFileName ?? System.IO.Path.GetFileName(model.FilePath);
                
                email.Attach(new Attachment
                {
                    Data = new System.IO.MemoryStream(fileBytes),
                    Filename = fileNameForAttachment,
                    ContentType = "application/pdf" 
                });

                var result = await email.SendAsync();

                if (result.Successful)
                {
                    emailLog.Status = 1;
                    await _context.SaveChangesAsync();
                    return true;
                }
                else
                {
                    var errorMessages = string.Join("; ", result.ErrorMessages);
                    emailLog.Status = 0;
                    emailLog.Content += $"\n[Lỗi gửi mail]: {errorMessages}";
                    await _context.SaveChangesAsync();
                    throw new Exception($"Gửi email thất bại: {errorMessages}");
                }
            }
            catch (Exception ex)
            {
                // Cập nhật log với thông tin lỗi Exception (nếu chưa có)
                emailLog.Status = 0;
                emailLog.Content += $"\n[Lỗi Exception]: {ex.Message}";
                await _context.SaveChangesAsync();
                if (ex.Message.StartsWith("Gửi email thất bại")) throw;
                throw new Exception($"Lỗi khi xử lý email: {ex.Message}", ex);
            }
        }
    }
}