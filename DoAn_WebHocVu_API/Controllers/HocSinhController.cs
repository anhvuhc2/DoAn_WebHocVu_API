using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DoAn_WebHocVu_API.Models;

namespace DoAn_WebHocVu_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "GiaoVien,HieuTruong")] // TẤT CẢ GIÁO VIÊN ĐỀU VÀO ĐƯỢC ĐÂY (Thỏa mãn điều kiện XEM ĐIỂM/DANH SÁCH)
    public class HocSinhController : ControllerBase
    {
        private readonly DoAnWebHocVuAdvancedContext _context;

        public HocSinhController(DoAnWebHocVuAdvancedContext context)
        {
            _context = context;
        }

        /// <summary>
        /// API dành cho Quản trị viên/BGH: Truy xuất toàn bộ hồ sơ lưu trữ của lớp
        /// Bao gồm cả học sinh đang học và học sinh đã chuyển trường/thôi học để làm báo cáo, thống kê.
        /// </summary>
        [HttpGet("truy-xuat-ho-so/{maLop}")]
        public async Task<IActionResult> TruyXuatHoSoTheoLop(string maLop)
        {
            // Lọc danh sách học sinh theo mã lớp được truyền lên từ giao diện
            var danhSach = await _context.HocSinhs
                .Where(hs => hs.MaLop == maLop)
                .ToListAsync();

            return Ok(danhSach);
        }
        /// <summary>
        /// API 2: Thêm mới học sinh (Chỉ GVCN lớp đó mới được thêm)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateHocSinh([FromBody] HocSinh hs)
        {
            // 1. Lấy mã giáo viên đang thao tác từ Token
            var maGiaoVien = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // 2. Tìm lớp học xem ai làm chủ nhiệm
            var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == hs.MaLop);
            if (lopHoc == null)
                return NotFound("Không tìm thấy mã lớp học này.");

            // 3. Kiểm tra xem giáo viên này có phải GVCN của lớp hoặc Hiệu trưởng không
            bool isHieuTruong = User.IsInRole("HieuTruong");
            if (lopHoc.GvchuNhiem != maGiaoVien && !isHieuTruong)
            {
                return StatusCode(403, new { message = $"Bạn không có quyền! Chỉ Hiệu trưởng hoặc GVCN của lớp {hs.MaLop} mới được phép thêm học sinh." });
            }

            // 4. Nếu đúng là GVCN -> Tiến hành thêm mới
            // Kiểm tra xem mã tài khoản phụ huynh nhập vào đã tồn tại trong bảng TaiKhoan chưa
            if (!string.IsNullOrEmpty(hs.TaiKhoanPhuHuynh))
            {
                var tkTonTai = await _context.TaiKhoans.AnyAsync(t => t.TenDangNhap == hs.TaiKhoanPhuHuynh);
                if (!tkTonTai)
                {
                    return BadRequest(new { message = $"Thất bại! Tài khoản phụ huynh '{hs.TaiKhoanPhuHuynh}' chưa tồn tại trong hệ thống. Vui lòng tạo tài khoản này trước." });
                }
            }
            _context.HocSinhs.Add(hs);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Thành công! Đã thêm học sinh {hs.HoTen} vào lớp {hs.MaLop}." });
        }

        /// <summary>
        /// API 3: Sửa thông tin học sinh (Chỉ GVCN lớp đó mới được sửa)
        /// </summary>
        [HttpPut("{maHS}")]
        public async Task<IActionResult> UpdateHocSinh(string maHS, [FromBody] HocSinh hsCapNhat)
        {
            var maGiaoVien = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Tìm học sinh gốc trong DB xem đang ở lớp nào trước khi sửa
            var hocSinhGoc = await _context.HocSinhs.FirstOrDefaultAsync(h => h.MaHs == maHS);
            if (hocSinhGoc == null)
                return NotFound("Không tìm thấy học sinh cần sửa.");

            // Check quyền chủ nhiệm lớp hiện tại của học sinh
            var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == hocSinhGoc.MaLop);
            bool isHieuTruong = User.IsInRole("HieuTruong");
            if (lopHoc == null || (lopHoc.GvchuNhiem != maGiaoVien && !isHieuTruong))
            {
                return StatusCode(403, new { message = $"Bạn không có quyền! Chỉ Hiệu trưởng hoặc GVCN của lớp {hocSinhGoc.MaLop} mới được phép sửa." });
            }

            // Tiến hành cập nhật thông tin
            hocSinhGoc.HoTen = hsCapNhat.HoTen;
            hocSinhGoc.NgaySinh = hsCapNhat.NgaySinh;
            hocSinhGoc.MaLop = hsCapNhat.MaLop; // Có thể chuyển lớp nếu giáo viên chủ nhiệm thao tác
            hocSinhGoc.TaiKhoanPhuHuynh = hsCapNhat.TaiKhoanPhuHuynh;

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Thành công! Đã cập nhật thông tin học sinh {maHS}." });
        }

        /// <summary>
        /// API 4: Xóa học sinh (Thực chất là chuyển trạng thái - Soft Delete)
        /// </summary>
        [HttpDelete("{maHS}")]
        public async Task<IActionResult> DeleteHocSinh(string maHS)
        {
            // 1. Lấy mã giáo viên chắc chắn 100%
            var maGiaoVien = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(maGiaoVien))
            {
                return StatusCode(401, new { message = "Lỗi Token: Không thể lấy được mã giáo viên từ thẻ đăng nhập!" });
            }

            // 2. Tìm học sinh
            var hocSinh = await _context.HocSinhs.FirstOrDefaultAsync(h => h.MaHs == maHS);
            if (hocSinh == null)
            {
                return NotFound("Không tìm thấy học sinh cần xóa.");
            }

            // 3. Kiểm tra quyền chủ nhiệm lớp và quyền Hiệu trưởng
            var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == hocSinh.MaLop);
            bool isHieuTruong = User.IsInRole("HieuTruong");
            if (lopHoc == null || (lopHoc.GvchuNhiem?.Trim().ToUpper() != maGiaoVien.Trim().ToUpper() && !isHieuTruong))
            {
                return StatusCode(403, new { message = $"Bạn không có quyền! Thao tác này chỉ dành cho Hiệu trưởng hoặc GVCN (hiện tại là '{lopHoc?.GvchuNhiem}')." });
            }

            // 4. THẦN THÁNH HÓA: Chuyển trạng thái để bảo toàn điểm số
            hocSinh.TrangThai = "Đã chuyển trường";

            await _context.SaveChangesAsync();

            return Ok(new { message = $"Thành công! Đã chuyển trạng thái hồ sơ của em {hocSinh.HoTen} thành 'Đã chuyển trường'." });
        }
    }
}