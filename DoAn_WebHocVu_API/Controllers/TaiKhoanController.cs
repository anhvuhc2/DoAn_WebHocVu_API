using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DoAn_WebHocVu_API.Models; // Kết nối tới thư mục Models của bạn

namespace DoAn_WebHocVu_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaiKhoanController : ControllerBase
    {
        private readonly DoAnWebHocVuAdvancedContext _context;
        private readonly IConfiguration _config;

        // Bốt bảo vệ cần 2 thứ: Kết nối CSDL (_context) và Sổ tay mật mã (_config)
        public TaiKhoanController(DoAnWebHocVuAdvancedContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("dang-nhap")]
        public IActionResult DangNhap([FromBody] LoginRequest request)
        {
            // 1. Dò tìm trong hệ thống xem có tài khoản nào khớp không
            // (Lưu ý: Tạm thời so sánh mật khẩu chữ thường, phần mã hóa tính sau)
            var user = _context.TaiKhoans.FirstOrDefault(t =>
                t.TenDangNhap == request.TenDangNhap && t.MatKhau == request.MatKhau);

            if (user == null)
            {
                return Unauthorized("Sai tên đăng nhập hoặc mật khẩu!"); // Báo lỗi 401
            }

            // 2. Tạo thông tin khắc lên thẻ (Claims)
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.TenDangNhap),
                new Claim(ClaimTypes.Name, user.HoTen),
                new Claim(ClaimTypes.Role, user.VaiTro) // Ghi rõ HieuTruong, GiaoVien hay PhuHuynh
            };

            // 3. Lấy con dấu bí mật từ appsettings.json
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // 4. Bắt đầu in thẻ
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(2), // Thẻ chỉ có hạn 2 tiếng cho an toàn
                signingCredentials: creds
            );

            // 5. Giao thẻ về cho Front-end (React)
            return Ok(new
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                VaiTro = user.VaiTro,
                HoTen = user.HoTen
            });
        } // <--- DẤU NGOẶC QUAN TRỌNG NHẤT: Đóng hàm Đăng Nhập ở đây!

        [HttpGet("kiem-tra-phan-cong")]
        public IActionResult KiemTraQuyen(string maGiaoVien, string maLop, string maMon)
        {
            // =========================================================================
            // TẦNG 1: KIỂM TRA GIÁO VIÊN BỘ MÔN (Ưu tiên kiểm tra phân công đích danh)
            // =========================================================================
            var laGiaoVienBoMon = _context.PhanCongGiangDays.Any(p =>
                p.MaGiaoVien == maGiaoVien && p.MaLop == maLop && p.MaMon == maMon);

            if (laGiaoVienBoMon)
            {
                return Ok(new { quyen = true, message = "Hợp lệ: Giáo viên bộ môn được phân công dạy môn này." });
            }

            // =========================================================================
            // TẦNG 2: KIỂM TRA GIÁO VIÊN CHỦ NHIỆM (Đặc quyền dạy các môn đại trà)
            // =========================================================================
            // Lấy thông tin lớp học ra để check giáo viên chủ nhiệm
            var lopHoc = _context.LopHocs.FirstOrDefault(l => l.MaLop == maLop);

            if (lopHoc != null && lopHoc.GvchuNhiem == maGiaoVien)
            {
                // GVCN có quyền dạy mọi môn đại trà, NGOẠI TRỪ các môn chuyên trách biệt lập
                if (maMon != "ANH" && maMon != "TIN")
                {
                    return Ok(new { quyen = true, message = "Hợp lệ: Giáo viên chủ nhiệm có quyền nhập điểm môn đại trà." });
                }
                else
                {
                    return BadRequest(new { quyen = false, message = "Thao tác bị chặn: Môn này đã có Giáo viên bộ môn chuyên trách đảm nhận!" });
                }
            }

            // =========================================================================
            // TẦNG 3: CHẶN ĐỨNG (Không phải GV bộ môn mà cũng chẳng phải GVCN của lớp)
            // =========================================================================
            return BadRequest(new { quyen = false, message = "Từ chối truy cập: Bạn không được phân công nhiệm vụ tại lớp này!" });
        }
    } // <--- Đóng lớp TaiKhoanController

    // Lớp phụ dùng để hứng dữ liệu Tài khoản/Mật khẩu do React gửi lên
    public class LoginRequest
    {
        public string TenDangNhap { get; set; } = null!;
        public string MatKhau { get; set; } = null!;
    }
} // <--- Đóng namespace