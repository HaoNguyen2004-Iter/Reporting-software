using System.Collections.Generic;
using System.Reflection.Emit;
using DBContext.Reportly.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DBContext.Reportly
{
    public class AppDBContext : DbContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options) : base(options) { }

        public DbSet<Email> EmailLogs => Set<Email>();
        public DbSet<Upload> Uploads => Set<Upload>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // EmailLogs
            modelBuilder.Entity<Email>(entity =>
            {
                entity.ToTable("EmailLogs");

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.ToEmail)
                      .IsRequired()
                      .HasMaxLength(256);

                entity.Property(e => e.CCEmail)
                      .HasMaxLength(1000);

                entity.Property(e => e.Subject)
                      .IsRequired()
                      .HasMaxLength(255);

                entity.Property(e => e.Content)
                      .IsRequired()
                      .HasColumnType("nvarchar(max)"); 

                entity.Property(e => e.Status).IsRequired();

                entity.Property(e => e.FilePath)
                      .HasMaxLength(512); 

                entity.Property(e => e.CreatedAt)
                      .HasColumnType("datetime")
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(e => e.CreatedBy)
                      .IsRequired()
                      .HasMaxLength(100);
            });

            // Uploads
            modelBuilder.Entity<Upload>(entity =>
            {
                entity.ToTable("Uploads");

                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.FileName)
                      .IsRequired()
                      .HasMaxLength(255);

                entity.Property(e => e.FilePath)
                      .IsRequired()
                      .HasMaxLength(512);

                entity.Property(e => e.FileExtension)
                      .IsRequired()
                      .HasMaxLength(16);

                entity.Property(e => e.FileSizeKB).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.CreateAt).HasColumnType("datetime");
                entity.Property(e => e.CreateBy).IsRequired();
            });
        }
    }
}