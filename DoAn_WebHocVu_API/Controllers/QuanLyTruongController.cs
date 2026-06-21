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
            // 1. KIỂM TRA LỚP CÓ TỒN TẠI KHÔNG
            var lop = await _context.LopHocs.FindAsync(maLop);
            if (lop == null) return NotFound(new { message = "Không tìm thấy lớp học" });

            // 2. GÁC CỔNG C#: Kiểm tra giáo viên này đã chủ nhiệm lớp khác chưa
            // (Điều kiện l.MaLop != maLop là để bỏ qua trường hợp cập nhật lại chính lớp đó)
            var daChuNhiemLopKhac = await _context.LopHocs
                .AnyAsync(l => l.GvchuNhiem == maGVCN && l.MaLop != maLop);

            if (daChuNhiemLopKhac)
            {
                return BadRequest(new { message = $"Giáo viên {maGVCN} hiện đang làm chủ nhiệm cho một lớp khác. Vui lòng chọn người khác!" });
            }

            // 3. TIẾN HÀNH CẬP NHẬT NẾU HỢP LỆ
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
        /// <summary>
        /// API: Hiệu trưởng cấp lại mật khẩu mặc định (123456) cho giáo viên bị quên
        /// </summary>
        [HttpPut("reset-mat-khau/{tenDangNhap}")]
        [Authorize(Roles = "HieuTruong")]
        public async Task<IActionResult> ResetMatKhauGiaoVien(string tenDangNhap)
        {
            var taiKhoan = await _context.TaiKhoans.FirstOrDefaultAsync(t => t.TenDangNhap == tenDangNhap);

            if (taiKhoan == null)
            {
                return NotFound(new { message = $"Không tìm thấy tài khoản {tenDangNhap} trong hệ thống." });
            }

            if (taiKhoan.VaiTro == "HieuTruong")
            {
                return BadRequest(new { message = "Không thể tự reset mật khẩu của tài khoản quản trị cấp cao." });
            }

            // Cấp lại mật khẩu mặc định
            taiKhoan.MatKhau = "123456";

            _context.TaiKhoans.Update(taiKhoan);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Đã reset mật khẩu của {tenDangNhap} thành công!",
                matKhauMoi = "123456",
                luuY = "Vui lòng yêu cầu đăng nhập và đổi mật khẩu ngay lập tức."
            });
        }
    }
}