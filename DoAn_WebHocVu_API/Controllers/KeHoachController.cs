using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAn_WebHocVu_API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using System.IO;
using System;
using System.Threading.Tasks;

namespace DoAn_WebHocVu_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class KeHoachController : ControllerBase
    {
        private readonly DoAnWebHocVuAdvancedContext _context;

        public KeHoachController(DoAnWebHocVuAdvancedContext context)
        {
            _context = context;
        }

        /// <summary>
        /// API Đăng kế hoạch lớp (Có hỗ trợ đính kèm file) - Thuộc 1 trong 4 tác vụ cốt lõi
        /// </summary>
        [HttpPost("dang-ke-hoach")]
        public async Task<IActionResult> DangKeHoach(
            [FromForm] string maLop,
            [FromForm] string tieuDe,
            [FromForm] string noiDung,
            [FromForm] string loaiThongBao,
            [FromForm] string? nguoiDang,
            IFormFile? fileDinhKem)
        {
            // 0. Khởi tạo đối tượng Kế hoạch chỉ với các thông tin thực sự cần thiết
            var keHoach = new KeHoachLop
            {
                MaLop = maLop,
                TieuDe = tieuDe,
                NoiDung = noiDung,
                LoaiThongBao = loaiThongBao,
                NguoiDang = nguoiDang
            };

            // 1. Xử lý lưu tệp đính kèm (nếu có)
            if (fileDinhKem != null && fileDinhKem.Length > 0)
            {
                string tenFile = Guid.NewGuid().ToString() + Path.GetExtension(fileDinhKem.FileName);
                string duongDan = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Uploads", tenFile);

                using (var stream = new FileStream(duongDan, FileMode.Create))
                {
                    await fileDinhKem.CopyToAsync(stream);
                }

                keHoach.FileDinhKem = "/Uploads/" + tenFile;
            }

            // 2. Ghi nhận thời gian đăng
            keHoach.NgayDang = DateTime.Now;
            // 3. Khởi tạo Transaction bảo vệ hệ thống
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Thêm vào bảng Kế Hoạch và lưu Database
                _context.KeHoachLops.Add(keHoach);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return Ok(new { message = "Đã đăng kế hoạch thành công!", data = keHoach });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi hệ thống: Dữ liệu bị hủy bỏ đồng bộ (Rollback) vì phát hiện tắc nghẽn giao dịch.", error = ex.Message });
            }
        }

        [HttpGet("danh-sach-lop/{maLop}")]
        [Authorize]
        public async Task<IActionResult> GetDanhSachKeHoachLop(string maLop)
        {
            var username = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }

            if (username.StartsWith("PH_"))
            {
                string maHs = username.Substring(3).Trim();
                var hs = await _context.HocSinhs.FirstOrDefaultAsync(h => h.MaHs == maHs);
                if (hs != null)
                {
                    maLop = hs.MaLop;
                }
            }

            if (string.IsNullOrEmpty(maLop)) return BadRequest("Lớp không hợp lệ!");
            string mlClean = maLop.Trim();

            IQueryable<KeHoachLop> query = _context.KeHoachLops;

            // Nếu là Giáo viên
            if (role == "GiaoVien")
            {
                string usrClean = username.Trim();
                
                // Kiểm tra xem giáo viên này có phải là GVCN của lớp này không
                var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop.Trim() == mlClean);
                bool isGVCN = lopHoc != null && lopHoc.GvchuNhiem?.Trim().ToUpper() == usrClean.ToUpper();

                if (isGVCN)
                {
                    // Nếu là GVCN, lấy toàn bộ kế hoạch của lớp (cả của GVCN và các GVBM đăng)
                    query = query.Where(kh => kh.MaLop.Trim() == mlClean);
                }
                else
                {
                    // Nếu là GVBM, chỉ lấy kế hoạch do chính họ đăng
                    query = query.Where(kh => kh.MaLop.Trim() == mlClean && kh.NguoiDang.Trim() == usrClean);
                }
            }
            else
            {
                query = query.Where(kh => kh.MaLop.Trim() == mlClean);
            }

            var danhSach = await query
                .OrderByDescending(kh => kh.NgayDang)
                .ToListAsync();
            return Ok(danhSach);
        }

        [HttpGet("tien-do-toan-truong")]
        public async Task<IActionResult> GetTienDoToanTruong([FromQuery] string loai = "gvcn")
        {
            if (loai == "gvbm")
            {
                // Thống kê kế hoạch của tất cả giáo viên bộ môn được phân công
                var danhSachPhanCong = await _context.PhanCongGiangDays
                    .Include(p => p.MaLopNavigation)
                    .Include(p => p.MaGiaoVienNavigation)
                    .Include(p => p.MaMonNavigation)
                    .ToListAsync();

                // Lọc bỏ trùng lặp theo tổ hợp Lớp - GV - Môn để tránh đếm trùng tiết
                var phanCongUnique = danhSachPhanCong
                    .GroupBy(p => new { p.MaLop, p.MaGiaoVien, p.MaMon })
                    .Select(g => g.First())
                    .ToList();

                var kq = new List<object>();

                foreach (var pc in phanCongUnique)
                {
                    if (string.IsNullOrEmpty(pc.MaLop) || string.IsNullOrEmpty(pc.MaGiaoVien)) continue;

                    string mlClean = pc.MaLop.Trim();
                    string gvClean = pc.MaGiaoVien.Trim();

                    // Tìm kế hoạch do chính GVBM này đăng cho lớp cụ thể này
                    var khMoiNhat = await _context.KeHoachLops
                        .Where(kh => kh.MaLop.Trim() == mlClean && kh.NguoiDang.Trim() == gvClean)
                        .OrderByDescending(kh => kh.NgayDang)
                        .FirstOrDefaultAsync();

                    var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == pc.MaLop);
                    var gv = await _context.TaiKhoans.FirstOrDefaultAsync(t => t.TenDangNhap == pc.MaGiaoVien);
                    var mon = await _context.MonHocs.FirstOrDefaultAsync(m => m.MaMon == pc.MaMon);

                    kq.Add(new {
                        maLop = pc.MaLop,
                        tenLop = lopHoc?.TenLop ?? pc.MaLop,
                        monHoc = mon?.TenMon ?? pc.MaMon,
                        maMon = pc.MaMon,
                        giaoVien = gv?.HoTen ?? pc.MaGiaoVien,
                        maGiaoVien = pc.MaGiaoVien,
                        trangThai = khMoiNhat != null ? "Đã nộp" : "Chưa có",
                        ngayNopGanNhat = khMoiNhat?.NgayDang,
                        tieuDeGanNhat = khMoiNhat?.TieuDe
                    });
                }

                return Ok(kq);
            }
            else
            {
                // Thống kê kế hoạch chủ nhiệm của GVCN
                var dsLop = await _context.LopHocs.ToListAsync();
                var kq = new List<object>();

                foreach(var l in dsLop)
                {
                    if (string.IsNullOrEmpty(l.MaLop)) continue;
                    string mlClean = l.MaLop.Trim();
                    string gvcnClean = (l.GvchuNhiem ?? "").Trim();

                    // Tìm kế hoạch do chính GVCN đăng cho lớp này
                    var khMoiNhat = await _context.KeHoachLops
                        .Where(kh => kh.MaLop.Trim() == mlClean && kh.NguoiDang.Trim() == gvcnClean)
                        .OrderByDescending(kh => kh.NgayDang)
                        .FirstOrDefaultAsync();

                    kq.Add(new {
                        maLop = l.MaLop,
                        tenLop = l.TenLop,
                        gvchuNhiem = l.GvchuNhiem,
                        trangThai = khMoiNhat != null ? "Đã nộp" : "Chưa có",
                        ngayNopGanNhat = khMoiNhat?.NgayDang,
                        tieuDeGanNhat = khMoiNhat?.TieuDe
                    });
                }

                return Ok(kq);
            }
        }

        [HttpPost("nhac-nho-kpi")]
        [Authorize(Roles = "HieuTruong")]
        public async Task<IActionResult> NhacNhoKpi([FromBody] NhacNhoKpiDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.MaGiaoVien) || string.IsNullOrEmpty(dto.MaLop))
            {
                return BadRequest("Dữ liệu nhắc nhở không đầy đủ!");
            }

            var gv = await _context.TaiKhoans.FirstOrDefaultAsync(t => t.TenDangNhap == dto.MaGiaoVien);
            var lop = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == dto.MaLop);
            if (gv == null || lop == null) return NotFound("Không tìm thấy giáo viên hoặc lớp học!");

            // BGH nhắc nhở trực tiếp tài khoản Giáo viên (sử dụng mã giáo viên làm TenDangNhap tương tác)
            string parentLoginName = dto.MaGiaoVien.Trim(); 

            string messageContent = "";
            if (!string.IsNullOrEmpty(dto.MaMon))
            {
                var mon = await _context.MonHocs.FirstOrDefaultAsync(m => m.MaMon == dto.MaMon);
                messageContent = $"[TO:{dto.MaGiaoVien.Trim()}] [HỆ THỐNG BGH ĐÔN ĐỐC] Thầy/Cô vui lòng cập nhật Kế hoạch tuần môn {mon?.TenMon ?? dto.MaMon} {lop.TenLop}!";
            }
            else
            {
                messageContent = $"[TO:{dto.MaGiaoVien.Trim()}] [HỆ THỐNG BGH ĐÔN ĐỐC] Thầy/Cô vui lòng cập nhật Kế hoạch chủ nhiệm/bảng điểm {lop.TenLop}!";
            }

            var tuongTac = new TuongTac
            {
                TenDangNhap = parentLoginName,
                NoiDung = messageContent,
                TrangThai = "Chờ GV xử lý",
                ThoiGian = DateTime.Now
            };

            _context.TuongTacs.Add(tuongTac);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Đã nhắc nhở thành công và gửi thông báo chuông tới thầy/cô {gv.HoTen}!" });
        }
    }

    public class NhacNhoKpiDto
    {
        public string MaGiaoVien { get; set; } = null!;
        public string MaLop { get; set; } = null!;
        public string? MaMon { get; set; }
    }
}