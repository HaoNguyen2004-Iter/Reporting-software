// Lấy danh sách

using DBContext.Reportly;
using DBContext.Reportly.Entities;
using Microsoft.EntityFrameworkCore;


namespace Service.Reportly.Executes.Reports
{
    public class ReportMany
    {
        private readonly AppDBContext _context;

        public ReportMany(AppDBContext context)
        {
            _context = context;
        }

        // Lấy danh sách bản nháp của user
        public async Task<List<Upload>> GetDraftsByUserAsync(int userId)
        {
            return await _context.Uploads
                .Where(u => u.CreatedBy == userId && u.Status == 1 && !_context.EmailLogs.Any(e => e.UploadId == u.Id))
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();
        }
       
    }
}