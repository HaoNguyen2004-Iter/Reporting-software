using System;

namespace DBContext.Reportly.Entities
{
    public class Upload
    {
        public int Id { get; set; }
        public string FileName { get; set; } = null!;
        public string FilePath { get; set; } = null!;
        public string FileExtension { get; set; } = null!;
        public int FileSizeKB { get; set; }
        public int Status { get; set; }
      
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public Guid ViewToken { get; set; }
        public string? CreatorFullName { get; set; }
        public string? CreatorDepartment { get; set; }


    }
}