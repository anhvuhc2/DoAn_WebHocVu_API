using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAn_WebHocVu_API.Models;

namespace DoAn_WebHocVu_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "HieuTruong")] // <--- Ổ KHÓA ĐỘC QUYỀN
    public class QuanLyTruongController : ControllerBase
    {
        private readonly DoAnWebHocVuAdvancedContext _context;

        public QuanLyTruongController(DoAnWebHocVuAdvancedContext context)
        {
            _context = context;
        }

        // 1. Lấy danh sách tất cả giáo viên để Hiệu trưởng chọn
        [HttpGet("danh-sach-giao-vien")]
        public async Task<IActionResult> GetGiaoVien()
        {
            var ds = await _context.TaiKhoans
                .Where(t => t.VaiTro == "GiaoVien")
                .Select(t => new { t.TenDangNhap, t.HoTen })
                .ToListAsync();
            return Ok(ds);
        }

        // 2. PHÂN CÔNG CHỦ NHIỆM (Cập nhật bảng LopHoc)
        [HttpPost("phan-cong-chu-nhiem")]
        public async Task<IActionResult> PhanCongChuNhiem(string maLop, string maGVCN)
        {
            var lop = await _context.LopHocs.FindAsync(maLop);
            if (lop == null) return NotFound(new { message = "Không tìm thấy lớp học" });

            lop.GvchuNhiem = maGVCN;
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Đã phân công {maGVCN} làm chủ nhiệm lớp {maLop}" });
        }

        // 3. PHÂN CÔNG BỘ MÔN (Thêm vào bảng PhanCongGiangDay)
        [HttpPost("phan-cong-bo-mon")]
        public async Task<IActionResult> PhanCongBoMon(PhanCongGiangDay pc)
        {
            // Kiểm tra xem đã tồn tại phân công này chưa để tránh trùng lặp
            var tonTai = await _context.PhanCongGiangDays.AnyAsync(p =>
                p.MaLop == pc.MaLop && p.MaMon == pc.MaMon);

            if (tonTai) return BadRequest(new { message = "Môn học này ở lớp này đã có giáo viên dạy rồi!" });

            _context.PhanCongGiangDays.Add(pc);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Phân công giáo viên bộ môn thành công!" });
        }
    }
}