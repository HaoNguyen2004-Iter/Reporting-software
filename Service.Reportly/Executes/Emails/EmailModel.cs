using System;

namespace Service.Reportly.Executes.Emails
{
    /// <summary>
    /// Model DTO cho việc gửi Email.
    /// </summary>
    public class EmailModels
    {
        public int Id { get; set; }
        public required string ToEmail { get; set; }
        public string? CCEmail { get; set; }
        public required string Subject { get; set; }
        public required string Content { get; set; }
        public int Status { get; set; }
   
        public int? UploadId { get; set; }
        
        public DateTime? CreatedAt { get; set; }
        public int CreatedBy { get; set; }   
        public string? SenderName { get; set; }
        public string? SenderDepartment { get; set; }
    }
}