using System;

namespace Service.Reportly.Executes.Emails
{
    /// <summary>
    /// Model DTO (Data Transfer Object) cho việc gửi Email
    /// </summary>
    public class EmailModels
    {
        public int Id { get; set; }
        public required string ToEmail { get; set; }
        public string? CCEmail { get; set; }
        public required string Subject { get; set; }
        public required string Content { get; set; }
        public int Status { get; set; }
        
        // Thông tin file (từ Form)
        /// <summary>
        /// Đường dẫn PHYSICAL dùng để đọc file đính kèm khi gửi mail (ví dụ: c:\\app\\wwwroot\\media\\upload\\...)
        /// </summary>
        public string? FilePath { get; set; }
        /// <summary>
        /// Đường dẫn PUBLIC lưu trong database (ví dụ: /media/upload/2025/11/07/file.pdf)
        /// </summary>
        public string? PublicFilePath { get; set; }
        public string? OriginalFileName { get; set; }
        public string? FileExtension { get; set; }
        public int? FileSizeKB { get; set; }
        /// <summary>
        /// Id bản ghi Upload (nếu có), để liên kết vào EmailLogs.UploadId
        /// </summary>
        public int? UploadId { get; set; }
        
        public DateTime? CreatedAt { get; set; }
        public int CreatedBy { get; set; }   
        public string? SenderName { get; set; }
        public string? SenderDepartment { get; set; }
    }
}