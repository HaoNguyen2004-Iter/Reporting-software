using DBContext.Reportly;
using DBContext.Reportly.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QRCoder;
using Rotativa.AspNetCore;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Service.Reportly.Executes.Reports
{
    public class ReportCommand
    {
        private readonly AppDBContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public ReportCommand(AppDBContext context, IWebHostEnvironment env, IConfiguration config)
        {
            _context = context;
            _env = env;
            _config = config;
        }

        public async Task<Upload> CreateDraftAsync(int userId, string htmlContent, string creatorName, string creatorDept)
        {
            // 1. Tạo record Draft
            var draft = new Upload
            {
                FileName = $"Draft_{DateTime.Now:yyyyMMddHHmmss}.pdf",
                FilePath = "", Status = 0, CreatedAt = DateTime.Now, CreatedBy = userId, FileExtension = ".pdf", FileSizeKB = 0,
                ViewToken = Guid.NewGuid(), CreatorFullName = creatorName, CreatorDepartment = creatorDept
            };
            _context.Uploads.Add(draft);
            await _context.SaveChangesAsync();

            // 2. Tạo QR & PDF
            var baseUrl = _config["AppSettings:BaseUrl"];
            if (string.IsNullOrEmpty(baseUrl)) baseUrl = "http://localhost:5000";

            var qrUrl = $"{baseUrl}/Report/Public/{draft.ViewToken}";
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrData);
            var qrBase64 = Convert.ToBase64String(qrCode.GetGraphic(20));
            var qrImgHtml = $"<img src=\"data:image/png;base64,{qrBase64}\" style=\"width: 120px; height: 120px;\" alt=\"QR Verify\" />";

            var finalHtml = htmlContent + $@"<div style='margin-top: 40px; padding-top: 20px; border-top: 2px solid #eee; text-align: center; page-break-inside: avoid;'><p style='margin-bottom: 10px; font-weight: bold; color: #555;'>XÁC THỰC BÁO CÁO</p>{qrImgHtml}<p style='margin-top: 10px; font-size: 12px; color: #999; font-family: monospace;'>REF: {draft.ViewToken.ToString().Substring(0, 8).ToUpper()}</p></div>";

            var now = DateTime.Now;
            var relativeFolder = $"/media/reports/{now.Year}/{now.Month:D2}";
            var physicalFolder = Path.Combine(_env.WebRootPath, relativeFolder.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(physicalFolder)) Directory.CreateDirectory(physicalFolder);

            var pdfName = $"Report_{draft.Id}_{now:yyyyMMddHHmmss}.pdf";
            var physicalPath = Path.Combine(physicalFolder, pdfName);
            
            var rotativaPath = Path.Combine(_env.WebRootPath, "Rotativa");
        var pdfBytes = WkhtmltopdfDriver.ConvertHtml(rotativaPath, GetPdfSwitches(), finalHtml);
            await File.WriteAllBytesAsync(physicalPath, pdfBytes);

            draft.FileName = pdfName;
            draft.FilePath = $"{relativeFolder}/{pdfName}";
            draft.FileSizeKB = (int)(new FileInfo(physicalPath).Length / 1024);
            draft.Status = 1;
            await _context.SaveChangesAsync();

            return draft;
        }



        private string GetPdfSwitches() => "--page-size A4 --margin-top 20mm --margin-right 20mm --margin-bottom 20mm --margin-left 20mm --encoding UTF-8 --print-media-type --enable-local-file-access";
    }
}