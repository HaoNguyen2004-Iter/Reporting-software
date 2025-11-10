using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Globalization;
using DBContext.Reportly;
using Web.Reportly.Controllers.Filters;
using Service.Reportly.Executes.Reports; 

namespace Web.Reportly.Controllers
{
    public class ReportController : BaseController
    {
        // Inject các Service đã hoàn tác
        private readonly ReportCommand _reportCommand;
        private readonly ReportOne _reportOne;
        private readonly ReportMany _reportMany;
        private readonly AppDBContext _context;

      
        public ReportController(
            ReportCommand reportCommand,
            ReportOne reportOne,
            ReportMany reportMany,
            AppDBContext context)
        {
            _reportCommand = reportCommand;
            _reportOne = reportOne;
            _reportMany = reportMany;
            _context = context;
        }

        // XEM CÔNG KHAI BẰNG TOKEN (DÙNG CHO QR CODE) ---
        [AllowAnonymous] 
        [HttpGet("Report/Public/{token}")]
        public async Task<IActionResult> PublicView(Guid token)
        {
            // Gọi Service ReportOne để lấy dữ liệu theo Token
            var upload = await _reportOne.GetByTokenAsync(token);

            if (upload == null)
            {
                return NotFound("Báo cáo không tồn tại hoặc đường dẫn không hợp lệ.");
            }

            ViewData["Title"] = "Xem báo cáo (Public)";
            ViewData["IsPublicView"] = true;
            
            // Tái sử dụng View "ViewReport" để hiển thị
            return View("ViewReport", upload);
        }

        //TẠO BÁO CÁO DRAFT 
        [HttpPost]
        public async Task<IActionResult> CreateReportDraft([FromBody] JsonElement body)
        {
            try
            {
                if (!body.TryGetProperty("html", out var htmlEl)) return BadRequest("Thiếu nội dung HTML.");
                var htmlContent = htmlEl.GetString();
                if (string.IsNullOrWhiteSpace(htmlContent)) return BadRequest("Nội dung trống.");

                var userId = HttpContext.Session.GetInt32("UserId");
                if (!userId.HasValue) return Unauthorized("Hết phiên đăng nhập.");

                var creatorName = HttpContext.Session.GetString("FullName") ?? "Không rõ";
                var creatorDept = HttpContext.Session.GetString("DepartmentName") ?? "N/A";

                // GỌI  SERVICE để tạo Draft
  
                var draft = await _reportCommand.CreateDraftAsync(userId.Value, htmlContent, creatorName, creatorDept);

                return Ok(new { 
                    success = true, 
                    uploadId = draft.Id, 
                    filePath = draft.FilePath, 
                    fileName = draft.FileName, 
                    message = "Đã lưu bản nháp thành công!" 
                });
            }
            catch (Exception ex)
            {
             
                return StatusCode(500, "Lỗi: " + ex.Message);
            }
        }

        // Action hiển thị trang viết báo cáo
        public IActionResult Write()
        {
            ViewData["Title"] = "Viết báo cáo";
            return View();
        }

        // Quản lý bản nháp (Sử dụng ReportMany Service)
        public async Task<IActionResult> Drafts()
        {
            ViewData["Title"] = "Quản lý bản nháp";
            var userId = HttpContext.Session.GetInt32("UserId");
            
            // Gọi Service lấy danh sách bản nháp của user hiện tại
            var drafts = await _reportMany.GetDraftsByUserAsync(userId.Value);
            
            return View(drafts);
        }

        
        //Xem chi tiết nội bộ 
        [HttpGet("Report/View/{id}")]
        public async Task<IActionResult> ViewReport(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            
            // Gọi Service ReportOne lấy theo ID
            var upload = await _reportOne.GetByIdAsync(id);

            if (upload == null) return NotFound("Không tìm thấy báo cáo.");
            
            // Kiểm tra quyền: chỉ người tạo mới được xem link này
            if (upload.CreatedBy != userId)
            {
                 TempData["Error"] = "Bạn không có quyền xem báo cáo này.";
                 return RedirectToAction("Index", "Home");
            }

            ViewData["Title"] = "Xem báo cáo";
            return View(upload);
        }

        // Nộp báo cáo 
        public async Task<IActionResult> Submit(int? draftId)
        {
            ViewData["Title"] = "Nộp báo cáo";
            var now = DateTime.Now;
            ViewData["CurrentWeek"] = $"Tuần {ISOWeek.GetWeekOfYear(now)} ({now:MM/yyyy})";

            if (draftId.HasValue)
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                // Gọi Service lấy thông tin draft để fill vào form
                var draft = await _reportOne.GetByIdAsync(draftId.Value);

                if (draft != null && draft.CreatedBy == userId)
                {
                    ViewData["SelectedDraft"] = draft;
                }
            }
            return View();
        }

        // lịch sử đã nộp 

        public async Task<IActionResult> History()
        {
            ViewData["Title"] = "Lịch sử báo cáo";
            var userId = HttpContext.Session.GetInt32("UserId");
            var logs = await _context.EmailLogs
                .Where(e => e.CreatedBy == userId)
                .OrderByDescending(e => e.CreatedAt)
                .Include(e => e.Upload)
                .ToListAsync();
            return View(logs);
        }

    // Chi tiết báo cáo
        [HttpGet("Report/Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var log = await _context.EmailLogs.Include(e => e.Upload).FirstOrDefaultAsync(e => e.Id == id);

            if (log == null || log.CreatedBy != userId)
            {
                TempData["Error"] = "Không tìm thấy báo cáo hoặc bạn không có quyền xem.";
                return RedirectToAction("History");
            }
            return View(log);
        }
    }
}