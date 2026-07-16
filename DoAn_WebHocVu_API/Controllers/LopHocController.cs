using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAn_WebHocVu_API.Models;

namespace DoAn_WebHocVu_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LopHocController : ControllerBase
    {
        private readonly DoAnWebHocVuAdvancedContext _context;

        public LopHocController(DoAnWebHocVuAdvancedContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 1. Xem danh sách lớp học (Mọi Giáo viên và Hiệu trưởng đều xem được)
        /// </summary>
        [HttpGet("danh-sach")]
        [Authorize(Roles = "HieuTruong,GiaoVien")]
        public async Task<IActionResult> LayDanhSachLop()
        {
            var dsLop = await _context.LopHocs.ToListAsync();
            return Ok(dsLop);
        }

        /// <summary>
        /// 2. Thêm lớp học mới (CHỈ HIỆU TRƯỞNG MỚI ĐƯỢC PHÉP)
        /// </summary>
        [HttpPost("them-moi")]
        [Authorize(Roles = "HieuTruong")] // <-- Ổ khóa cấp 1: Chặn đứng giáo viên
        public async Task<IActionResult> ThemLopMoi([FromBody] LopHoc lopMoi)
        {
            var daTonTai = await _context.LopHocs.AnyAsync(l => l.MaLop == lopMoi.MaLop);
            if (daTonTai)
            {
                return BadRequest(new { message = $"Lỗi: Mã lớp '{lopMoi.MaLop}' đã tồn tại!" });
            }

            _context.LopHocs.Add(lopMoi);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Thành công: Đã tạo lớp {lopMoi.TenLop} (Mã: {lopMoi.MaLop})." });
        }

        /// <summary>
        /// API lấy danh sách chính xác giáo viên của lớp (Phục vụ sổ thả Parent)
        /// </summary>
        [HttpGet("danh-sach-giao-vien/{maLop}")]
        [Authorize(Roles = "HieuTruong,GiaoVien,PhuHuynh")]
        public async Task<IActionResult> DanhSachGiaoVienCuaLop(string maLop)
        {
            var username = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(username))
            {
                username = username.Trim();
                if (username.StartsWith("PH_"))
                {
                    string maHs = username.Substring(3).Trim();
                    var hs = await _context.HocSinhs.FirstOrDefaultAsync(h => h.MaHs.Trim() == maHs);
                    if (hs != null)
                    {
                        maLop = hs.MaLop.Trim();
                    }
                }
            }

            if (!string.IsNullOrEmpty(maLop)) maLop = maLop.Trim();
            var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop.Trim() == maLop);
            if (lopHoc == null) return NotFound(new { message = "Lớp không tồn tại" });

            var ketQua = new List<object>();

            if (!string.IsNullOrEmpty(lopHoc.GvchuNhiem))
            {
                var tkGvcn = await _context.TaiKhoans.FirstOrDefaultAsync(t => t.TenDangNhap.Trim() == lopHoc.GvchuNhiem.Trim());
                if (tkGvcn != null)
                {
                    ketQua.Add(new { tenDangNhap = tkGvcn.TenDangNhap.Trim(), hoTen = tkGvcn.HoTen.Trim(), chucVu = "Giáo viên Chủ nhiệm" });
                }
            }

            var maGvbms = await _context.PhanCongGiangDays
                .Where(p => p.MaLop.Trim() == maLop && p.MaGiaoVien.Trim() != lopHoc.GvchuNhiem.Trim())
                .Select(p => new { MaGiaoVien = p.MaGiaoVien.Trim(), MaMon = p.MaMon.Trim() })
                .ToListAsync();

            foreach (var pc in maGvbms)
            {
                var tkGvbm = await _context.TaiKhoans.FirstOrDefaultAsync(t => t.TenDangNhap.Trim() == pc.MaGiaoVien);
                if (tkGvbm != null)
                {
                    var mon = await _context.MonHocs.FirstOrDefaultAsync(m => m.MaMon.Trim() == pc.MaMon);
                    ketQua.Add(new { tenDangNhap = tkGvbm.TenDangNhap.Trim(), hoTen = tkGvbm.HoTen.Trim(), chucVu = "GVBM " + (mon?.TenMon ?? pc.MaMon).Trim() });
                }
            }
            var distinctKetQua = ketQua.GroupBy(x => ((dynamic)x).tenDangNhap).Select(g => g.First()).ToList();
            return Ok(distinctKetQua);
        }

        /// <summary>
        /// API dành cho Giáo viên: Lấy danh sách sĩ số hiện tại của lớp (Chỉ gồm học sinh đang học)
        /// Phục vụ cho tác vụ hàng ngày: Điểm danh, nhập điểm, gửi thông báo.
        /// </summary>
        [HttpGet("{maLop}/danh-sach-hien-tai")]
        [Authorize(Roles = "HieuTruong,GiaoVien")]
        public async Task<IActionResult> LayDanhSachHocSinhHienTai(string maLop)
        {
            // BỔ SUNG ĐIỀU KIỆN: hs.TrangThai == "Đang học"
            var dsHocSinh = await _context.HocSinhs
                                        .Where(hs => hs.MaLop == maLop && hs.TrangThai == "Đang học")
                                        .ToListAsync();

            if (dsHocSinh.Count == 0)
            {
                return Ok(new { message = $"Lớp {maLop} hiện tại không có học sinh nào đang theo học." });
            }
            return Ok(dsHocSinh);
        }

        /// <summary>
        /// 4. Điểm danh (Nhận danh sách vắng từ Front-end và lưu vào DB)
        /// </summary>
        [HttpPost("{maLop}/diem-danh")]
        [Authorize(Roles = "GiaoVien")]
        public async Task<IActionResult> DiemDanhLop(string maLop, [FromBody] List<ThongTinVang> danhSachVang)
        {
            try
            {
                // BƯỚC 1: Lấy mã ID chuẩn xác 100%
                var maGvDangDangNhap = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(maGvDangDangNhap))
                {
                    return StatusCode(401, new { message = "Lỗi Token: Không thể lấy được mã giáo viên từ thẻ đăng nhập!" });
                }

                // BƯỚC 2: Tìm lớp học để đối chiếu
                var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == maLop);
                if (lopHoc == null)
                {
                    return NotFound(new { message = "Không tìm thấy lớp học này!" });
                }

                // BƯỚC 3: VÒNG BẢO VỆ 2 - Kiểm tra quyền
                if (lopHoc.GvchuNhiem?.Trim().ToUpper() != maGvDangDangNhap.Trim().ToUpper())
                {
                    return StatusCode(403, new { message = $"CẢNH BÁO: Bạn không phải là giáo viên chủ nhiệm của lớp {lopHoc.TenLop}." });
                }

                // BƯỚC 4: XỬ LÝ LƯU VÀO DATABASE (Phiên bản "Gạn đục khơi trong")
                var danhSachHsHopLe = await _context.HocSinhs
                     .Where(h => h.MaLop == maLop && h.TrangThai == "Đang học")
                     .Select(h => h.MaHs).ToListAsync();
                var ngayHienTai = DateOnly.FromDateTime(DateTime.Now);

                int soLuongThanhCong = 0;
                List<string> danhSachLoi = new List<string>();

                foreach (var hs in danhSachVang)
                {
                    // CHỐT CHẶN 3: Nếu phát hiện đi lạc lớp
                    if (!danhSachHsHopLe.Contains(hs.MaHs))
                    {
                        danhSachLoi.Add(hs.MaHs);
                        continue;
                    }

                    var diemDanhMoi = new DiemDanh
                    {
                        MaHs = hs.MaHs,
                        NgayVang = ngayHienTai,
                        TrangThai = hs.TrangThai,
                        NguoiDiemDanh = maGvDangDangNhap
                    };

                    // CHỐT CHẶN: Kiểm tra xem học sinh này đã bị điểm danh hôm nay chưa
                    var daVangRoi = await _context.DiemDanhs
                    .AnyAsync(dd => dd.MaHs == hs.MaHs && dd.NgayVang == ngayHienTai);

                    if (daVangRoi)
                    {
                        // Nếu điểm danh rồi thì ném vào danh sách lỗi, không cho Add xuống DB
                        danhSachLoi.Add(hs.MaHs + " (Bị trùng)");
                        continue; // Bỏ qua em này, tiếp tục vòng lặp cho các em khác
                    }
                    _context.DiemDanhs.Add(diemDanhMoi);
                    soLuongThanhCong++;
                }

                await _context.SaveChangesAsync();

                if (danhSachLoi.Count > 0)
                {
                    return Ok(new { message = $"Đã điểm danh thành công {soLuongThanhCong} học sinh. LƯU Ý: Đã từ chối {danhSachLoi.Count} học sinh vì không thuộc lớp này hoặc bị trùng({string.Join(", ", danhSachLoi)})." });
                }

                return Ok(new { message = $"Tuyệt vời! Đã ghi nhận thành công toàn bộ {soLuongThanhCong} học sinh vắng mặt của lớp {lopHoc.TenLop}." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Lỗi máy chủ C#: {ex.Message} - {ex.InnerException?.Message}" });
            }
        }

        /// <summary>
        /// 5. Tổng hợp điểm danh (Cung cấp dữ liệu Chuyên cần cho Bảng Điểm)
        /// </summary>
        [HttpGet("{maLop}/tong-hop-diem-danh")]
        [Authorize(Roles = "HieuTruong,GiaoVien")]
        public async Task<IActionResult> TongHopDiemDanhLop(string maLop)
        {
            var dsHocSinh = await _context.HocSinhs
                 .Where(h => h.MaLop == maLop)
                 .Select(h => h.MaHs).ToListAsync();

            var tatCaDiemDanh = await _context.DiemDanhs
                 .Where(d => dsHocSinh.Contains(d.MaHs))
                 .ToListAsync();

            var ketQua = dsHocSinh.Select(maHs => {
                var vangs = tatCaDiemDanh.Where(d => d.MaHs == maHs).ToList();
                return new {
                    maHs = maHs,
                    tongVang = vangs.Count,
                    coPhep = vangs.Count(d => d.TrangThai == "Có phép"),
                    khongPhep = vangs.Count(d => d.TrangThai == "Không phép"),
                    chiTiet = vangs.Select(v => new { ngay = v.NgayVang, trangThai = v.TrangThai }).OrderByDescending(v => v.ngay).ToList()
                };
            }).ToList();

            return Ok(ketQua);
        }

        public class ThongTinVang
        {
            public string MaHs { get; set; }
            public string TrangThai { get; set; }
        }
    }
}