using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DoAn_WebHocVu_API.Models;

namespace DoAn_WebHocVu_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "GiaoVien")] // TẤT CẢ GIÁO VIÊN ĐỀU VÀO ĐƯỢC ĐÂY (Thỏa mãn điều kiện XEM ĐIỂM/DANH SÁCH)
    public class HocSinhController : ControllerBase
    {
        private readonly DoAnWebHocVuAdvancedContext _context;

        public HocSinhController(DoAnWebHocVuAdvancedContext context)
        {
            _context = context;
        }

        /// <summary>
        /// API 1: Xem danh sách học sinh (Mọi giáo viên đều xem được)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetHocSinhs()
        {
            var danhSach = await _context.HocSinhs.ToListAsync();
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

            // 3. Kiểm tra xem giáo viên này có phải GVCN của lớp không
            if (lopHoc.GvchuNhiem != maGiaoVien)
            {
                return StatusCode(403, new { message = $"Bạn không có quyền! Chỉ GVCN của lớp {hs.MaLop} mới được phép thêm học sinh." });
            }

            // 4. Nếu đúng là GVCN -> Tiến hành thêm mới
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
            if (lopHoc == null || lopHoc.GvchuNhiem != maGiaoVien)
            {
                return StatusCode(403, new { message = $"Bạn không có quyền! Chỉ GVCN của lớp {hocSinhGoc.MaLop} mới được phép sửa." });
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
        /// API 4: Xóa học sinh (Chỉ GVCN lớp đó mới được xóa)
        /// </summary>
        [HttpDelete("{maHS}")]
        public async Task<IActionResult> DeleteHocSinh(string maHS)
        {
            var maGiaoVien = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var hocSinh = await _context.HocSinhs.FirstOrDefaultAsync(h => h.MaHs == maHS);
            if (hocSinh == null)
                return NotFound("Không tìm thấy học sinh cần xóa.");

            // Check quyền chủ nhiệm
            var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == hocSinh.MaLop);
            if (lopHoc == null || lopHoc.GvchuNhiem != maGiaoVien)
            {
                return StatusCode(403, new { message = $"Bạn không có quyền! Chỉ GVCN của lớp {hocSinh.MaLop} mới được phép xóa học sinh này." });
            }

            _context.HocSinhs.Remove(hocSinh);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Thành công! Đã xóa học sinh {maHS} khỏi hệ thống." });
        }
    }
}