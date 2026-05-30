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
        /// 3. Xem danh sách học sinh của 1 lớp (Chỉ lấy những em Đang học)
        /// </summary>
        [HttpGet("{maLop}/hoc-sinh")]
        [Authorize(Roles = "HieuTruong,GiaoVien")]
        public async Task<IActionResult> LayHocSinhCuaLop(string maLop)
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

                _context.DiemDanhs.Add(diemDanhMoi);
                soLuongThanhCong++;
            }

            await _context.SaveChangesAsync();

            if (danhSachLoi.Count > 0)
            {
                return Ok(new { message = $"Đã điểm danh thành công {soLuongThanhCong} học sinh. LƯU Ý: Đã từ chối {danhSachLoi.Count} học sinh vì không thuộc lớp này ({string.Join(", ", danhSachLoi)})." });
            }

            return Ok(new { message = $"Tuyệt vời! Đã ghi nhận thành công toàn bộ {soLuongThanhCong} học sinh vắng mặt của lớp {lopHoc.TenLop}." });
        }

        public class ThongTinVang
        {
            public string MaHs { get; set; }
            public string TrangThai { get; set; }
        }
    }
}