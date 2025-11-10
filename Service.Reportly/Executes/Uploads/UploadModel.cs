using System;

namespace Service.Reportly.Executes.Uploads
{
    public class UploadModel
    {
        public int Id { get; set; }
        public string FileName { get; set; } = null!;
        public string FilePath { get; set; } = null!;
        public string FileExtension { get; set; } = null!;
        public int FileSizeKB { get; set; }
        public int Status { get; set; }
       
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
    }
}