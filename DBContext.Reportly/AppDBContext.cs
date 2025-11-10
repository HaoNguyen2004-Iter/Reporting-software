using DBContext.Reportly.Entities;
using Microsoft.EntityFrameworkCore;

namespace DBContext.Reportly
{
    public class AppDBContext : DbContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }

        public DbSet<Email> EmailLogs => Set<Email>();
       public DbSet<Upload> Uploads => Set<Upload>();
 

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Good practice

            // --- EmailLogs Configurations ---
            modelBuilder.Entity<Email>(entity =>
            {
                entity.ToTable("EmailLogs");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ToEmail).IsRequired().HasMaxLength(256);
                entity.Property(e => e.CCEmail).HasMaxLength(1000);
                entity.Property(e => e.Subject).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Content).IsRequired(); // nvarchar(max) là mặc định cho string không giới hạn
                entity.Property(e => e.Status).IsRequired();

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("datetime")
                      .HasDefaultValueSql("GETDATE()");

                // Sửa lại mapping cho đúng kiểu int
                entity.Property(e => e.CreatedBy).IsRequired();

                entity.Property(e => e.SenderName).HasMaxLength(100);
                entity.Property(e => e.SenderDepartment).HasMaxLength(100);

                // Cấu hình Relationship (Foreign Key)
                entity.HasOne(e => e.Upload)
                      .WithMany() // Một Upload có thể được tham chiếu bởi nhiều Email (nếu muốn 1-1 thì dùng WithOne)
                      .HasForeignKey(e => e.UploadId)
                      .OnDelete(DeleteBehavior.Restrict); // Quan trọng: Tránh xóa Upload làm lỗi Email log
            });

            // --- Uploads Configurations ---
            modelBuilder.Entity<Upload>(entity =>
            {
                entity.ToTable("Uploads");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(512);
                entity.Property(e => e.FileExtension).IsRequired().HasMaxLength(16);
                entity.Property(e => e.FileSizeKB).IsRequired();
                entity.Property(e => e.Status).IsRequired();

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("datetime")
                      .HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.CreatedBy).IsRequired();
                entity.Property(e => e.ViewToken).IsRequired();
                entity.Property(e => e.CreatorFullName).HasMaxLength(100);
                entity.Property(e => e.CreatorDepartment).HasMaxLength(100);
       
            });

         
        }
    }
}