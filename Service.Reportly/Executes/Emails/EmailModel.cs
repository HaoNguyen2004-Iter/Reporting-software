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
        public string? FilePath { get; set; }
        public string? OriginalFileName { get; set; }
        public string? FileExtension { get; set; }
        public int? FileSizeKB { get; set; }
        
        public DateTime? CreatedAt { get; set; }
        public int CreatedBy { get; set; }   
        public string? SenderName { get; set; }
        public string? SenderDepartment { get; set; }
    }
}