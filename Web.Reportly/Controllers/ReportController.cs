using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;
using DBContext.Reportly; 
using Microsoft.EntityFrameworkCore; 
using System.Globalization; 
using Web.Reportly.Controllers.Filters; 

namespace Web.Reportly.Controllers
{
    //  Kế thừa từ BaseController để tự động kiểm tra Session
    public class ReportController : BaseController
    {
        private readonly AppDBContext _context;
        public ReportController(AppDBContext context)
        {
            _context = context;
        }

        public IActionResult Write()
        {
            ViewData["Title"] = "Viết báo cáo";
            ViewData["PageTitle"] = "Viết báo cáo tuần";
            ViewData["PageSubtitle"] = "Tạo và gửi báo cáo tuần của bạn";
            return View();
        }

        public IActionResult Submit()
        {
            ViewData["Title"] = "Nộp báo cáo";
            ViewData["PageTitle"] = "Nộp báo cáo tuần";
            ViewData["PageSubtitle"] = "Gửi báo cáo đã hoàn thành đến người quản lý";
            
            // (Code tính tuần vẫn giữ nguyên)
            var culture = new CultureInfo("vi-VN");
            var now = DateTime.Now;
            DayOfWeek startOfWeek = DayOfWeek.Monday;
            int diff = (7 + (now.DayOfWeek - startOfWeek)) % 7;
            var monday = now.AddDays(-1 * diff).Date;
            var sunday = monday.AddDays(6);
            int weekNumber = ISOWeek.GetWeekOfYear(now);
            string weekString = $"Tuần {weekNumber} ({monday:dd/MM/yyyy} – {sunday:dd/MM/yyyy})";
            ViewData["CurrentWeek"] = weekString;

            return View();
        }

        // Action Lịch sử (List)
        public async Task<IActionResult> History()
        {
            ViewData["Title"] = "Lịch sử báo cáo";
            ViewData["PageTitle"] = "Lịch sử báo cáo";
            ViewData["PageSubtitle"] = "Xem lại các báo cáo đã gửi";

            // SỬA: Lấy UserId (INT) từ Session (đã được kiểm tra bởi BaseController)
            var userId = HttpContext.Session.GetInt32("UserId");

            // SỬA: Lọc theo UserId (INT)
            var logs = await _context.EmailLogs
                .Where(e => e.CreatedBy == userId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            return View(logs); 
        }

        // Action Chi tiết
        [HttpGet("Report/Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            ViewData["Title"] = "Chi tiết báo cáo";
            ViewData["PageTitle"] = "Chi tiết báo cáo";
            ViewData["PageSubtitle"] = "Xem lại nội dung báo cáo đã gửi";

            // SỬA: Lấy UserId (INT) từ Session
            var userId = HttpContext.Session.GetInt32("UserId");

            var log = await _context.EmailLogs.FindAsync(id);

            // SỬA: Kiểm tra bảo mật bằng UserId (INT)
            if (log == null || log.CreatedBy != userId)
            {
                TempData["Error"] = "Không tìm thấy báo cáo hoặc bạn không có quyền xem.";
                return RedirectToAction("History");
            }

            return View(log);
        }


        [HttpPost]
        public IActionResult UploadReport([FromBody] JsonElement body)
        {
            // (Action này không đổi)
            if (!body.TryGetProperty("html", out var htmlEl))
                return BadRequest("Missing 'html'.");
            var html = htmlEl.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(html))
                return BadRequest("Content is empty.");
            
            // ... (Rotativa code) ...
            var pdf = new ViewAsPdf("Pdf", html)
            {
                FileName = $"BaoCaoTuan-{DateTime.Now:yyyyMMdd-HHmm}.pdf",
                PageSize = Size.A4,
                PageOrientation = Orientation.Portrait,
                PageMargins = new Margins { Left = 12, Right = 12, Top = 10, Bottom = 16 },
                CustomSwitches = "--print-media-type --enable-local-file-access"
            };

            return pdf;
        }
    }
}