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

        public async Task<bool> SendAsync(EmailModels model, string publicPath)
        {

            if (SqlGuard.IsSuspicious(model))
                throw new Exception("Đầu vào không hợp lệ");

            // Bắt đầu Transaction để lưu vào 2 bảng
            using var transaction = await _context.Database.BeginTransactionAsync();
            
            DBContext.Reportly.Entities.Email emailLog;

            try
            {
                // === BƯỚC 1: TẠO RECORD UPLOADS ===
                var newUpload = new DBContext.Reportly.Entities.Upload
                {
                    FileName = model.OriginalFileName ?? "N/A",
                    FilePath = publicPath, // Đường dẫn public 
                    FileExtension = model.FileExtension ?? ".pdf",
                    FileSizeKB = model.FileSizeKB ?? 0,
                    Status = 1, // 1 = Đã nộp
                    CreateAt = DateTime.Now,
                    CreateBy = model.CreatedBy 
                };
                
                _context.Uploads.Add(newUpload);
                await _context.SaveChangesAsync(); 
               

                // === BƯỚC 2: TẠO RECORD EMAILLOGS ===
                emailLog = new DBContext.Reportly.Entities.Email
                {
                    ToEmail = model.ToEmail,
                    CCEmail = model.CCEmail,
                    Subject = model.Subject,
                    Content = model.Content,
                    Status = 0, 
                    FilePath = publicPath, // Đường dẫn public
                    OriginalFileName = model.OriginalFileName,
                    CreatedAt = DateTime.Now,
                    CreatedBy = model.CreatedBy,
                    UploadId = newUpload.Id, 

            
                    SenderName = model.SenderName,
                    SenderDepartment = model.SenderDepartment
                };

                _context.EmailLogs.Add(emailLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Lỗi lưu database (Transaction Rollback): {ex.Message}", ex);
            }

            // === BƯỚC 3: GỬI EMAIL ===
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

                // === BƯỚC 4: CẬP NHẬT TRẠNG THÁI ===
                if (result.Successful)
                {
                    emailLog.Status = 1;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync(); 
                    return true;
                }
                else
                {
                    emailLog.Status = 0; 
                    var errorMessages = string.Join("; ", result.ErrorMessages);
                    emailLog.Content += $"\n[Lỗi gửi mail]: {errorMessages}";
                    
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync(); // Vẫn Commit dù gửi thất bại (để lưu log lỗi)
                    
                    throw new Exception($"Gửi email thất bại: {errorMessages}");
                }
            }
            catch (Exception ex)
            {
                // Xử lý nếu BƯỚC 3 (Gửi Email) bị lỗi
                emailLog.Status = 0;
                emailLog.Content += $"\n[Lỗi Exception]: {ex.Message}";
                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // Vẫn Commit log lỗi

                if (ex.Message.StartsWith("Gửi email thất bại"))
                    throw; // Gửi lại lỗi "Gửi email thất bại"
                    
                throw new Exception($"Lỗi khi xử lý email: {ex.Message}", ex);
            }
        }
    }
}