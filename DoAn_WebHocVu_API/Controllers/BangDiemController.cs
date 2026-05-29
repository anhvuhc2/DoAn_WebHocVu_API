using DoAn_WebHocVu_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
// Đừng quên using thư mục Models của bạn vào đây nhé

namespace DoAn_WebHocVu_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "GiaoVien")] // CHỐT CHẶN VÒNG NGOÀI: Phải có thẻ Giáo Viên mới được gọi API này
    public class BangDiemController : ControllerBase
    {
        private readonly DoAnWebHocVuAdvancedContext _context;

        public BangDiemController(DoAnWebHocVuAdvancedContext context)
        {
            _context = context;
        }

        [HttpPost("nhap-diem")]
        public async Task<IActionResult> NhapDiem(string maHS, string maMon, float diemMoi)
        {
            // 1. Lấy mã giáo viên đang đăng nhập từ Token (Giả sử bạn lưu Username vào Name)
            var maGiaoVien = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // 2. Lấy thông tin để đối chiếu
            var monHoc = await _context.MonHocs.FirstOrDefaultAsync(m => m.MaMon == maMon);
            var hocSinh = await _context.HocSinhs.FirstOrDefaultAsync(h => h.MaHs == maHS);

            if (monHoc == null || hocSinh == null)
                return NotFound("Không tìm thấy môn học hoặc học sinh.");

            // 3. THUẬT TOÁN GÁC CỔNG VÒNG TRONG (Kiểm tra chéo)
            bool duocPhepThaoTac = false;

            if (monHoc.LoaiMon == "Cơ bản")
            {
                // LUẬT 1: Môn cơ bản -> Đi tìm lớp của học sinh này xem ai làm chủ nhiệm
                var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == hocSinh.MaLop);
                if (lopHoc != null && lopHoc.GvchuNhiem == maGiaoVien)
                {
                    duocPhepThaoTac = true; // Khớp GVCN -> Mở cổng
                }
            }
            else if (monHoc.LoaiMon == "Chuyên")
            {
                // LUẬT 2: Môn chuyên -> Lục bảng Phân Công Giảng Dạy xem có tên không
                var duocPhanCong = await _context.PhanCongGiangDays
                    .AnyAsync(pc => pc.MaGiaoVien == maGiaoVien && pc.MaLop == hocSinh.MaLop && pc.MaMon == maMon);

                if (duocPhanCong)
                {
                    duocPhepThaoTac = true; // Khớp phân công bộ môn -> Mở cổng
                }
            }

            // 4. Phán quyết cuối cùng
            if (!duocPhepThaoTac)
            {
                return StatusCode(403, new { message = "Lỗi phân quyền: Bạn không có quyền nhập điểm cho môn này của lớp này!" });
            }

            // --- NẾU CODE CHẠY ĐƯỢC XUỐNG ĐÂY NGHĨA LÀ ĐÃ QUA CỬA BẢO MẬT ---
            // (Bạn sẽ viết code thêm/sửa/lưu dữ liệu vào bảng BangDiem ở khu vực này)

            
            // 5. Tìm xem học sinh này đã có điểm môn này trong bảng chưa
            var bangDiem = await _context.BangDiems
                .FirstOrDefaultAsync(b => b.MaHs == maHS && b.MaMon == maMon);

            if (bangDiem == null)
            {
                // Nếu chưa có điểm -> Tạo dòng điểm mới (Thêm)
                bangDiem = new BangDiem
                {
                    MaHs = maHS,
                    MaMon = maMon,
                    DiemThi = diemMoi, // Lưu ý: Tùy biến cột này theo đúng tên cột điểm trong SQL của bạn
                    NgayCapNhat = DateTime.Now
                };
                _context.BangDiems.Add(bangDiem);
            }
            else
            {
                // Nếu đã có điểm rồi -> Ghi đè điểm mới (Sửa)
                bangDiem.DiemThi = diemMoi;
                bangDiem.NgayCapNhat = DateTime.Now;
                _context.BangDiems.Update(bangDiem);
            }

            // 6. Lưu vào Database
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Thành công! Đã cập nhật {diemMoi} điểm cho học sinh {maHS} môn {monHoc.TenMon}." });
        }
        [HttpGet("xem-diem/{maHS}")]
        public async Task<IActionResult> XemDiem(string maHS)
        {
            var bangDiem = await _context.BangDiems
                .Where(b => b.MaHs == maHS)
                .Select(b => new
                {
                    MaMon = b.MaMon,
                    TenMon = _context.MonHocs.FirstOrDefault(m => m.MaMon == b.MaMon).TenMon,
                    DiemThi = b.DiemThi,
                    NgayCapNhat = b.NgayCapNhat
                })
                .ToListAsync();

            if (!bangDiem.Any())
                return NotFound(new { message = $"Học sinh {maHS} hiện chưa có điểm nào trong hệ thống." });

            return Ok(bangDiem);
        }
    }
}