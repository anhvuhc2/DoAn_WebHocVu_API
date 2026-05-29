using Microsoft.AspNetCore.Authorization;
using DoAn_WebHocVu_API.Models; // Kết nối tới thư mục Models của bạn

using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;

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
        /// <summary>
        /// API 1: Xem danh sách toàn bộ tài khoản 
        /// </summary>
        [HttpGet("danh-sach")]
        [Authorize(Roles = "HieuTruong, GiaoVien")]
        public async Task<IActionResult> LayDanhSachTaiKhoan()
        {
            var danhSach = await _context.TaiKhoans.ToListAsync();
            return Ok(danhSach);
        }

        /// <summary>
        /// API 2: Thêm nhân sự mới (Chỉ Hiệu trưởng)
        /// </summary>
        [HttpPost("them-tai-khoan")]
        [Authorize(Roles = "HieuTruong")] // Gắn mác VIP: Cửa này chỉ Hiệu trưởng được vào
        public async Task<IActionResult> ThemTaiKhoan([FromBody] TaiKhoan tkMoi)
        {
            // Kiểm tra xem mã nhân sự này đã bị trùng chưa
            var daTonTai = await _context.TaiKhoans.AnyAsync(t => t.TenDangNhap == tkMoi.TenDangNhap);
            if (daTonTai)
            {
                return BadRequest(new { message = $"Lỗi: Mã tài khoản '{tkMoi.TenDangNhap}' đã tồn tại!" });
            }

            _context.TaiKhoans.Add(tkMoi);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Tuyệt vời! Đã cấp tài khoản {tkMoi.VaiTro} cho {tkMoi.HoTen}." });
        }

        /// <summary>
        /// API 3: Xóa nhân sự nghỉ việc / chuyển trường (Chỉ Hiệu trưởng)
        /// </summary>
        [HttpDelete("xoa-tai-khoan/{tenDangNhap}")]
        [Authorize(Roles = "HieuTruong")]
        public async Task<IActionResult> XoaTaiKhoan(string tenDangNhap)
        {
            var taiKhoan = await _context.TaiKhoans.FirstOrDefaultAsync(t => t.TenDangNhap == tenDangNhap);
            if (taiKhoan == null)
            {
                return NotFound(new { message = "Không tìm thấy tài khoản này trong hệ thống." });
            }

            // Chống xóa nhầm chính quyền của Hiệu trưởng đang đăng nhập
            var userDangNhap = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (taiKhoan.TenDangNhap == userDangNhap)
            {
                return BadRequest(new { message = "Không thể tự xóa tài khoản của chính mình đang đăng nhập!" });
            }

            _context.TaiKhoans.Remove(taiKhoan);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Đã xóa thành công tài khoản {tenDangNhap} khỏi hệ thống." });
        }

        [HttpGet("kiem-tra-phan-cong")]
        [Authorize(Roles = "HieuTruong,GiaoVien")]
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
        /// <summary>
        /// API Xem danh sách các lớp và môn được phân công của 1 giáo viên
        /// <summary>
        /// API Xem danh sách các lớp và môn được phân công của 1 giáo viên
        /// </summary>
        [HttpGet("lich-day/{maGiaoVien}")]
        [Authorize(Roles = "HieuTruong,GiaoVien")] // Sếp và đồng nghiệp đều xem được
        public async Task<IActionResult> XemLichDay(string maGiaoVien)
        {
            // BƯỚC 1: KIỂM TRA XEM MÃ NÀY CÓ TỒN TẠI KHÔNG VÀ CÓ PHẢI LÀ GIÁO VIÊN KHÔNG
            var giaoVien = await _context.TaiKhoans.FirstOrDefaultAsync(t => t.TenDangNhap == maGiaoVien);

            if (giaoVien == null)
            {
                return NotFound(new { message = $"Lỗi: Giáo viên có mã '{maGiaoVien}' không tồn tại trong hệ thống!" });
            }

            if (giaoVien.VaiTro != "GiaoVien")
            {
                return BadRequest(new { message = $"Lỗi: Tài khoản '{maGiaoVien}' không mang chức vụ Giáo Viên!" });
            }

            // BƯỚC 2: NẾU TỒN TẠI, BẮT ĐẦU TÌM LỊCH DẠY
            var lichDay = await _context.PhanCongGiangDays
                                        .Where(p => p.MaGiaoVien == maGiaoVien)
                                        .ToListAsync();

            if (lichDay.Count == 0)
            {
                // Gọi luôn tên thật của giáo viên cho thân thiện
                return Ok(new { message = $"Giáo viên {giaoVien.HoTen} hiện tại chưa được phân công dạy môn nào." });
            }

            return Ok(lichDay);
        }

    } // <--- Đóng lớp TaiKhoanController

    // Lớp phụ dùng để hứng dữ liệu Tài khoản/Mật khẩu do React gửi lên
    public class LoginRequest
    {
        public string TenDangNhap { get; set; } = null!;
        public string MatKhau { get; set; } = null!;

    }
} // <--- Đóng namespace