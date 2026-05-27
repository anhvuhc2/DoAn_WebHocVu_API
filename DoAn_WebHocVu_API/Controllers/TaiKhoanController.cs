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
            // Tìm trong bảng Phân công xem có dòng nào khớp không
            var check = _context.PhanCongGiangDays.Any(p =>
                p.MaGiaoVien == maGiaoVien && p.MaLop == maLop && p.MaMon == maMon);

            if (check)
            {
                return Ok(new { quyen = true });
            }
            else
            {
                return BadRequest(new { quyen = false, message = "Bạn không được phân công dạy môn này ở lớp này!" });
            }
        }
    } // <--- Đóng lớp TaiKhoanController

    // Lớp phụ dùng để hứng dữ liệu Tài khoản/Mật khẩu do React gửi lên
    public class LoginRequest
    {
        public string TenDangNhap { get; set; } = null!;
        public string MatKhau { get; set; } = null!;
    }
} // <--- Đóng namespace