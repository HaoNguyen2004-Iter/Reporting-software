// Lấy 1 bản ghi theo ID hoặc Token


using DBContext.Reportly;
using DBContext.Reportly.Entities;
using Microsoft.EntityFrameworkCore;

namespace Service.Reportly.Executes.Reports
{
    public class ReportOne
    {
        private readonly AppDBContext _context;

        public ReportOne(AppDBContext context)
        {
            _context = context;
        }

        // Lấy theo ID (dùng cho trang Details/View nội bộ)
        public async Task<Upload?> GetByIdAsync(int id)
        {
            return await _context.Uploads.FindAsync(id);
        }

        // Lấy theo Token (dùng cho trang Public quét QR)
        public async Task<Upload?> GetByTokenAsync(Guid token)
        {
            return await _context.Uploads.FirstOrDefaultAsync(u => u.ViewToken == token);
        }

    }
}