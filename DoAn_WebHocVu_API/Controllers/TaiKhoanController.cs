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
        
        // =========================================================================
        // API CHO DEVELOPER: LẤY TOÀN BỘ TÀI KHOẢN ĐỂ GẮN VÀO TOOL MOCK LOGIN
        // =========================================================================
        [HttpGet("all-for-testing")]
        public async Task<IActionResult> GetAllForTesting()
        {
            var ds = await _context.TaiKhoans.Select(t => new { t.TenDangNhap, t.HoTen, t.VaiTro }).ToListAsync();
            return Ok(ds);
        }

        // =========================================================================
        // API: DANH SÁCH GIÁO VIÊN (ĐÃ TÍCH HỢP THUẬT TOÁN ĐỌC NHIỆM VỤ & LỊCH DẠY)
        // =========================================================================
        [HttpGet("danh-sach-giao-vien")]
        [Authorize(Roles = "HieuTruong,GiaoVien")]
        public async Task<IActionResult> LayDanhSachGiaoVien()
        {
            // BƯỚC 1: Lấy dữ liệu an toàn từ Database (Chỉ lọc lấy Giáo viên, bỏ qua Phụ huynh)
            var danhSachTho = await _context.TaiKhoans
                .Where(t => t.VaiTro == "GiaoVien")
                .Select(tk => new
                {
                    tk.TenDangNhap,
                    tk.HoTen,
                    tk.VaiTro,
                    PhanCongGiangDays = tk.PhanCongGiangDays.Select(pc => new
                    {
                        MaLop = pc.MaLop,
                        MaMon = pc.MaMon
                    }).ToList()
                })
                .ToListAsync();

            // BƯỚC 2: Chế biến thêm cột "NhiemVu" ngay trên RAM của máy chủ
            var danhSachHoanThien = danhSachTho.Select(tk => new
            {
                HoTen = tk.HoTen,
                VaiTro = tk.VaiTro,
                // THUẬT TOÁN ĐỌC TÊN:
                NhiemVu = tk.TenDangNhap.StartsWith("GVCN")
                    ? $"Giáo viên chủ nhiệm {tk.TenDangNhap.Substring(4)}"
                    : (tk.PhanCongGiangDays.Count > 0 ? "Giáo viên bộ môn" : "Chưa phân công"),
                PhanCongGiangDays = tk.PhanCongGiangDays
            });

            return Ok(danhSachHoanThien);
        }

        /// <summary>
        /// API 2: Thêm nhân sự mới (Chỉ Hiệu trưởng)
        /// </summary>
        // =========================================================================
        // API 2: THÊM TÀI KHOẢN MỚI (Tự động kiểm tra MaHS & Liên kết tự động cho GVCN)
        // =========================================================================
        [HttpPost("them-tai-khoan")]
        [Authorize(Roles = "HieuTruong,GiaoVien")]
        // 💡 THÊM THAM SỐ maHS (MÃ HỌC SINH) ĐỂ KIỂM TRA QUYỀN VÀ TỰ ĐỘNG GẮN BẢNG HỌC SINH
        public async Task<IActionResult> ThemTaiKhoan([FromBody] TaiKhoan tkMoi, [FromQuery] string? maHS = null)
        {
            // Kiểm tra xem mã tài khoản này đã bị trùng chưa
            var daTonTai = await _context.TaiKhoans.AnyAsync(t => t.TenDangNhap == tkMoi.TenDangNhap);
            if (daTonTai)
            {
                return BadRequest(new { message = $"Lỗi: Mã tài khoản '{tkMoi.TenDangNhap}' đã tồn tại!" });
            }

            // =========================================================================
            // 🛡️ TẦNG 2: KIỂM TRA QUYỀN CỦA GIÁO VIÊN CHỦ NHIỆM DỰA TRÊN MÃ HỌC SINH
            // =========================================================================
            if (!User.IsInRole("HieuTruong")) // Nếu là Giáo viên đang đăng nhập
            {
                // Luật 1: GVCN tuyệt đối chỉ được phép tạo tài khoản Phụ huynh
                if (tkMoi.VaiTro != "PhuHuynh")
                {
                    return StatusCode(403, new { message = "⛔ Bạn chỉ có quyền tạo tài khoản Phụ huynh, không thể tự ý tạo tài khoản Giáo viên hoặc Ban giám hiệu!" });
                }

                // Luật 2: GVCN khi tạo tài khoản Phụ huynh BẮT BUỘC phải truyền vào Mã Học Sinh (maHS)
                if (string.IsNullOrEmpty(maHS))
                {
                    return BadRequest(new { message = "⛔ Vui lòng nhập thêm Mã học sinh (maHS) để hệ thống kiểm tra quyền Chủ nhiệm của bạn!" });
                }

                // Luật 3: Kiểm tra xem Mã học sinh có tồn tại trong hệ thống không
                var hocSinh = await _context.HocSinhs.FirstOrDefaultAsync(hs => hs.MaHs == maHS);
                if (hocSinh == null)
                {
                    return NotFound(new { message = $"⛔ Không tìm thấy học sinh nào có mã '{maHS}' trong hệ thống!" });
                }

                // Luật 4: Kiểm tra xem học sinh này có thuộc lớp do cô này chủ nhiệm không
                var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == hocSinh.MaLop);
                var userDangNhap = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (lopHoc == null || lopHoc.GvchuNhiem != userDangNhap)
                {
                    return StatusCode(403, new { message = $"⛔ Từ chối! Học sinh {hocSinh.HoTen} thuộc lớp {hocSinh.MaLop}, bạn không phải GVCN lớp này nên không được phép tạo tài khoản phụ huynh cho em này!" });
                }
            }
            // =========================================================================

            // BƯỚC 1: LƯU TÀI KHOẢN MỚI VÀO DATABASE
            _context.TaiKhoans.Add(tkMoi);
            await _context.SaveChangesAsync();

            // BƯỚC 2: SIÊU TIỆN ÍCH - TỰ ĐỘNG GẮN MÃ PHỤ HUYNH VÀO BẢNG HỌC SINH LUÔN!
            if (!string.IsNullOrEmpty(maHS))
            {
                var hocSinhCanGan = await _context.HocSinhs.FirstOrDefaultAsync(hs => hs.MaHs == maHS);
                if (hocSinhCanGan != null)
                {
                    hocSinhCanGan.TaiKhoanPhuHuynh = tkMoi.TenDangNhap;
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new { message = $"Tuyệt vời! Đã cấp tài khoản {tkMoi.VaiTro} cho {tkMoi.HoTen} và tự động liên kết thành công với học sinh mã {maHS}!" });
        }

        /// <summary>
        /// API 3: Xóa nhân sự nghỉ việc / chuyển trường (Chỉ Hiệu trưởng)
        /// </summary>
        [HttpDelete("xoa-tai-khoan/{tenDangNhap}")]
        [Authorize(Roles = "HieuTruong,GiaoVien")] // Chỉ Hiệu trưởng mới có quyền này
        public async Task<IActionResult> XoaTaiKhoan(string tenDangNhap)
        {
            string maClean = tenDangNhap.Trim();
            var taiKhoan = await _context.TaiKhoans.FirstOrDefaultAsync(t => t.TenDangNhap == maClean);
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
            // =========================================================================
            // 🛡️ TẦNG 2: KIỂM TRA QUYỀN CỦA GIÁO VIÊN CHỦ NHIỆM
            // =========================================================================
            if (!User.IsInRole("HieuTruong")) // Nếu KHÔNG PHẢI là Hiệu trưởng (tức là Giáo viên)
            {
                // Luật 1: GVCN chỉ được xóa tài khoản Phụ huynh, không được xóa đồng nghiệp/Sếp
                if (taiKhoan.VaiTro != "PhuHuynh")
                {
                    return StatusCode(403, new { message = "⛔ Bạn chỉ có quyền xóa tài khoản Phụ huynh, không thể xóa tài khoản đồng nghiệp hoặc Ban giám hiệu!" });
                }

                // Luật 2: Phụ huynh đó phải có con đang học trong lớp do chính mình chủ nhiệm          
                var hocSinh = await _context.HocSinhs.FirstOrDefaultAsync(hs => hs.TaiKhoanPhuHuynh == maClean);
                if (hocSinh != null)
                {
                    var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == hocSinh.MaLop);

                    // Tận dụng luôn biến userDangNhap đã khai báo sẵn ở dòng 142 bên trên
                    if (lopHoc == null || lopHoc.GvchuNhiem != userDangNhap)
                    {
                        return StatusCode(403, new { message = $"⛔ Từ chối! Phụ huynh này thuộc lớp {hocSinh.MaLop}, bạn không phải GVCN lớp này nên không thể xóa!" });
                    }
                }
            }
            // =========================================================================
            // =========================================================================
            // 1. DỌN DẸP LIÊN KẾT: Ngắt liên kết với bảng Học Sinh (Cho bất kỳ role nào)
            // =========================================================================
            var cacHocSinh = await _context.HocSinhs
                .Where(hs => hs.TaiKhoanPhuHuynh == taiKhoan.TenDangNhap)
                .ToListAsync();

            foreach (var hs in cacHocSinh)
            {
                hs.TaiKhoanPhuHuynh = null; // Cắt đứt liên kết tài khoản, giữ nguyên dữ liệu học sinh
            }
            // =========================================================================
            // 2. DỌN DẸP LIÊN KẾT NHÂN SỰ: Gỡ chức chủ nhiệm & lịch giảng dạy (Không phân biệt rác dữ liệu)
            // =========================================================================
            var lopChuNhiems = await _context.LopHocs
                .Where(l => l.GvchuNhiem == taiKhoan.TenDangNhap )
                .ToListAsync();
            foreach (var lop in lopChuNhiems)
            {
                lop.GvchuNhiem = null;
            }

            var phanCongs = await _context.PhanCongGiangDays
                .Where(pc => pc.MaGiaoVien == taiKhoan.TenDangNhap)
                .ToListAsync();
            if (phanCongs.Any())
            {
                _context.PhanCongGiangDays.RemoveRange(phanCongs);
            }
            // =========================================================================
            // 3. DỌN DẸP CHUNG: Ngắt khóa ngoại và xóa các tương tác
            // =========================================================================
            // 3.1 Dọn Tương Tác
            var tuongTacs = await _context.TuongTacs
                .Where(t => t.TenDangNhap == taiKhoan.TenDangNhap)
                .ToListAsync();
            if (tuongTacs.Any())
            {
                _context.TuongTacs.RemoveRange(tuongTacs);
            }
            
            // 3.2 Ngắt liên kết Lịch sử Đăng Kế hoạch
            var cacKeHoach = await _context.KeHoachLops.Where(k => k.NguoiDang == taiKhoan.TenDangNhap).ToListAsync();
            foreach (var k in cacKeHoach)
            {
                k.NguoiDang = null;
            }

            // 3.3 Ngắt liên kết Lịch sử Điểm danh
            var cacDiemDanh = await _context.DiemDanhs.Where(d => d.NguoiDiemDanh == taiKhoan.TenDangNhap).ToListAsync();
            foreach (var d in cacDiemDanh)
            {
                d.NguoiDiemDanh = null;
            }

            // =========================================================================
            // 4. BƯỚC CUỐI: Nhổ cọc - Xóa tài khoản gốc khỏi hệ thống
            // =========================================================================
            _context.TaiKhoans.Remove(taiKhoan);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Đã xóa thành công tài khoản '{tenDangNhap}' ra khỏi hệ thống!" });
        }


        [HttpGet("kiem-tra-phan-cong")]
        [Authorize(Roles = "HieuTruong,GiaoVien")]
        public IActionResult KiemTraQuyen(string maGiaoVien, string maLop, string maMon)
        {
            string mgvTrim = maGiaoVien?.Trim().ToUpper() ?? "";
            string mlTrim = maLop?.Trim().ToUpper() ?? "";
            string mmTrim = maMon?.Trim().ToUpper() ?? "";

            // =========================================================================
            // TẦNG 1: KIỂM TRA GIÁO VIÊN BỘ MÔN (Ưu tiên kiểm tra phân công đích danh)
            // =========================================================================
            var laGiaoVienBoMon = _context.PhanCongGiangDays.Any(p =>
                p.MaGiaoVien.Trim().ToUpper() == mgvTrim && 
                p.MaLop.Trim().ToUpper() == mlTrim && 
                p.MaMon.Trim().ToUpper() == mmTrim);

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
                // Thay vì hardcode môn, tra cứu bảng Phân công xem môn này đã có ai dạy ở lớp này chưa
                var daCoGVBMAssign = _context.PhanCongGiangDays.Any(p => p.MaLop.Trim().ToUpper() == mlTrim && p.MaMon.Trim().ToUpper() == mmTrim);
                
                if (!daCoGVBMAssign)
                {
                    return Ok(new { quyen = true, message = "Hợp lệ: Giáo viên chủ nhiệm bao sô giảng dạy các môn chưa phân công." });
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

            // BƯỚC 3: XỬ LÝ LỊCH ẢO - CẤP CHO GVCN NHỮNG MÔN 'BAO SÔ'
            var cacLopChuNhiem = await _context.LopHocs
                .Where(l => l.GvchuNhiem.Trim().ToUpper() == maGiaoVien.Trim().ToUpper())
                .ToListAsync();

            if (cacLopChuNhiem.Any())
            {
                var tatCaMon = await _context.MonHocs.ToListAsync();
                foreach (var lop in cacLopChuNhiem)
                {
                    // Lấy các môn đã CÓ người dạy ở lớp này
                    var cacMonDaPhanCong = await _context.PhanCongGiangDays
                        .Where(p => p.MaLop == lop.MaLop)
                        .Select(p => p.MaMon.Trim().ToUpper())
                        .ToListAsync();

                    // Xác định danh sách môn tự động được áp cho lớp này theo khối
                    bool laKhoi12 = lop.MaLop.Contains("1") || lop.MaLop.Contains("2");
                    bool laKhoi3 = lop.MaLop.Contains("3");
                    
                    var maMonChuan = new List<string> { "TOAN", "TV", "DD", "GDTC", "AN", "MT", "HĐTN" };
                    if (laKhoi12) { maMonChuan.Add("TNXH"); maMonChuan.Add("ANH"); }
                    else if (laKhoi3) { maMonChuan.Add("TNXH"); maMonChuan.Add("ANH"); maMonChuan.Add("TIN"); maMonChuan.Add("CN"); }
                    else { maMonChuan.Add("KH"); maMonChuan.Add("LSĐL"); maMonChuan.Add("ANH"); maMonChuan.Add("TIN"); maMonChuan.Add("CN"); }

                    foreach (var mm in maMonChuan)
                    {
                        if (!cacMonDaPhanCong.Contains(mm))
                        {
                            var monObj = tatCaMon.FirstOrDefault(m => m.MaMon.Trim().ToUpper() == mm);
                            lichDay.Add(new PhanCongGiangDay
                            {
                                MaPhanCong = -1,
                                MaGiaoVien = maGiaoVien,
                                MaLop = lop.MaLop,
                                MaMon = monObj != null ? monObj.MaMon : mm,
                                Thu = "Cả tuần",
                                Buoi = "Linh hoạt",
                                Tiet = null
                            });
                        }
                    }
                }
            }

            if (lichDay.Count == 0)
            {
                // Gọi luôn tên thật của giáo viên cho thân thiện
                return Ok(new { message = $"Giáo viên {giaoVien.HoTen} hiện tại chưa được phân công dạy môn nào." });
            }

            return Ok(lichDay);
        }

        /// <summary>
        /// API: Cấp lại mật khẩu mặc định (123456) cho Phụ huynh (Đã chốt chặn quyền GVCN)
        /// </summary>
        [HttpPut("reset-mat-khau-phu-huynh/{maHs}")]
        [Authorize(Roles = "GiaoVien,HieuTruong")]
        public async Task<IActionResult> ResetMatKhauPhuHuynh(string maHs)
        {
            // Lấy thông tin người đang đăng nhập
            var maNguoiDung = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var vaiTro = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            // 1. Tìm thông tin học sinh
            var hocSinh = await _context.HocSinhs.FirstOrDefaultAsync(h => h.MaHs == maHs);
            if (hocSinh == null)
            {
                return NotFound(new { message = "Không tìm thấy mã học sinh này!" });
            }

            // --- BƯỚC RÀO CHẮN AN NINH ---
            var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == hocSinh.MaLop);
            // Nếu là Giáo viên thì bắt buộc phải là GVCN của lớp này mới được phép
            if (lopHoc != null && vaiTro != "HieuTruong")
            {
                if (lopHoc.GvchuNhiem?.Trim().ToUpper() != maNguoiDung?.Trim().ToUpper())
                {
                    return StatusCode(403, new { message = $"TỪ CHỐI: Bạn không phải Giáo viên chủ nhiệm của lớp {lopHoc.TenLop}. Chỉ GVCN mới có quyền reset mật khẩu cho phụ huynh lớp mình!" });
                }
            }

            // 2. Kiểm tra xem phụ huynh em này đã có tài khoản chưa
            if (string.IsNullOrEmpty(hocSinh.TaiKhoanPhuHuynh))
            {
                return BadRequest(new { message = $"Phụ huynh của em {hocSinh.HoTen} chưa được cấp tài khoản để reset!" });
            }

            // 3. Tìm đúng tài khoản đó trong bảng TaiKhoan
            var taiKhoan = await _context.TaiKhoans.FirstOrDefaultAsync(t => t.TenDangNhap == hocSinh.TaiKhoanPhuHuynh);
            if (taiKhoan == null)
            {
                return NotFound(new { message = "Lỗi hệ thống: Tài khoản tồn tại ở bảng học sinh nhưng không có trong bảng tài khoản." });
            }

            // 4. Reset về mặc định
            taiKhoan.MatKhau = "123456";

            _context.TaiKhoans.Update(taiKhoan);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Đã reset mật khẩu cho phụ huynh em {hocSinh.HoTen} thành công!",
                tenDangNhap = taiKhoan.TenDangNhap,
                matKhauMoi = "123456",
                luuY = "GVCN vui lòng nhắc phụ huynh đổi mật khẩu ngay sau khi đăng nhập."
            });
        }    
               
        // =========================================================================
        // 2. API: DANH SÁCH PHỤ HUYNH THEO LỚP (Bảo mật 2 tầng: Role + Đúng GVCN)
        // =========================================================================
        [HttpGet("danh-sach-phu-huynh/theo-lop/{maLop}")]
        [Authorize(Roles = "HieuTruong,GiaoVien")]
        public async Task<IActionResult> GetDanhSachPhuHuynhTheoLop(string maLop)
        {
            // =====================================================================
            // 🛡️ TẦNG 2: KIỂM TRA QUYỀN CHỦ NHIỆM (Chỉ áp dụng với Giáo viên)
            // =====================================================================
            // Nếu người đang đăng nhập KHÔNG PHẢI là Hiệu trưởng -> Bắt buộc phải kiểm tra lớp
            if (!User.IsInRole("HieuTruong"))
            {
                // Lấy tên tài khoản đang đăng nhập từ Token JWT
                var userDangNhap = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                // Tìm thông tin lớp học trong DB
                var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == maLop);

                // Nếu lớp không tồn tại hoặc tài khoản này KHÔNG phải là GVCN của lớp đó -> Chặn ngay!
                if (lopHoc == null || lopHoc.GvchuNhiem != userDangNhap)
                {
                    return StatusCode(403, new
                    {
                        message = $"⛔ Từ chối truy cập! Tài khoản '{userDangNhap}' không phải là Giáo viên chủ nhiệm của lớp {maLop}."
                    });
                }
            }
            // =====================================================================
            // Bước 1: Quét bảng Học sinh xem lớp này có những tài khoản phụ huynh nào
            var taiKhoanPHs = await _context.HocSinhs
                .Where(hs => hs.MaLop == maLop && !string.IsNullOrEmpty(hs.TaiKhoanPhuHuynh))
                .Select(hs => hs.TaiKhoanPhuHuynh)
                .Distinct()
                .ToListAsync();

            if (!taiKhoanPHs.Any())
            {
                return Ok(new
                {
                    message = $"Lớp '{maLop}' hiện chưa có tài khoản phụ huynh nào được đăng ký.",
                    data = new List<object>()
                });
            }

            // Bước 2: Lấy thông tin chi tiết tài khoản phụ huynh từ bảng TaiKhoans
            var phuHuynhs = await _context.TaiKhoans
                .Where(t => taiKhoanPHs.Contains(t.TenDangNhap))
                .ToListAsync();

            return Ok(phuHuynhs);
        }
        // Lớp phụ dùng để hứng dữ liệu Tài khoản/Mật khẩu do React gửi lên
        public class LoginRequest
    {
        public string TenDangNhap { get; set; } = null!;
        public string MatKhau { get; set; } = null!;

    }
        

} // <--- ĐÂY LÀ DẤU NGOẶC NHỌN ĐÓNG CUỐI CÙNG CỦA CLASS TAIKHOANCONTROLLER

} // <--- Đóng namespace