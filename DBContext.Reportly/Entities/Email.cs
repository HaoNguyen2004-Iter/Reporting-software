using System;

namespace DBContext.Reportly.Entities
{
    public class Email
    {
        public int Id { get; set; }
        public string ToEmail { get; set; } = null!;
        public string? CCEmail { get; set; }
        public string Subject { get; set; } = null!;
        public string Content { get; set; } = null!;
        public int Status { get; set; }
        
        public string? FilePath { get; set; }
        public string? OriginalFileName { get; set; }

        public int? UploadId { get; set; } 
        
        public int CreatedBy { get; set; } 

        public string? SenderName { get; set; } 
        public string? SenderDepartment { get; set; } 
        
        public DateTime? CreatedAt { get; set; }

     
    }
}