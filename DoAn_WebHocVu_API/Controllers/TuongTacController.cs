using DoAn_WebHocVu_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace DoAn_WebHocVu_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TuongTacController : ControllerBase
    {
        private readonly DoAnWebHocVuAdvancedContext _context;

        public TuongTacController(DoAnWebHocVuAdvancedContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Webhook: Hứng tin nhắn phản hồi từ Phụ huynh (Zalo/App)
        /// </summary>
        [HttpPost("gui-phan-hoi")]
        public async Task<IActionResult> PostPhanHoi([FromBody] TuongTac phanHoi)
        {
            if (phanHoi == null) return BadRequest("Dữ liệu phản hồi không được để trống!");

            // 1. Tìm kế hoạch gốc
            var keHoachGoc = await _context.KeHoachLops.FindAsync(phanHoi.MaKeHoach);
            if (keHoachGoc == null) return NotFound("Không tìm thấy kế hoạch này.");

            // 2. Logic AI Thông tư 27 (Đã xử lý an toàn giá trị Null)
            string noiDungThuong = (phanHoi.NoiDung ?? "").ToLower();
            string phanHoiCuaHeThong = "";

            if (keHoachGoc.LoaiThongBao != null && keHoachGoc.LoaiThongBao.Trim() == "Báo điểm")
            {
                bool hoiMonNhanXet = noiDungThuong.Contains("đạo đức") || noiDungThuong.Contains("thể dục") ||
                                     noiDungThuong.Contains("âm nhạc") || noiDungThuong.Contains("mỹ thuật") ||
                                     noiDungThuong.Contains("mĩ thuật") || noiDungThuong.Contains("hoạt động trải nghiệm") ||
                                     noiDungThuong.Contains("tự nhiên và xã hội") || noiDungThuong.Contains("môn phụ");

                bool thacMacDanhGia = noiDungThuong.Contains("h ") || noiDungThuong.Contains("ngoan") || noiDungThuong.Contains("điểm");

                if (hoiMonNhanXet && thacMacDanhGia)
                {
                    phanHoiCuaHeThong = "Trợ lý ảo: Dạ thưa phụ huynh, theo Thông tư 27, các môn như Đạo đức, Thể dục, Hoạt động trải nghiệm... được đánh giá qua quan sát biểu hiện trên lớp, không dùng điểm số. Mức H (Hoàn thành) có nghĩa là em đã đạt các yêu cầu cơ bản. Để đạt T (Hoàn thành Tốt) đòi hỏi thêm sự nổi trội trong hoạt động. GVCN sẽ trao đổi chi tiết hơn với anh/chị ạ!";
                    phanHoi.TrangThai = "Chờ GV xử lý";
                }
                else if (noiDungThuong.Contains("xếp loại") || noiDungThuong.Contains("điểm h") || noiDungThuong.Contains("điểm t") || noiDungThuong.Contains("điểm c"))
                {
                    phanHoiCuaHeThong = "Trợ lý ảo: Dạ thưa phụ huynh, theo Thông tư 27: T là 'Hoàn thành Tốt', H là 'Hoàn thành', và C là 'Chưa hoàn thành' mục tiêu môn học ạ.";
                    phanHoi.TrangThai = "AI đã trả lời";
                }
                else
                {
                    phanHoiCuaHeThong = "Trợ lý ảo: Dạ thắc mắc của anh/chị về tình hình học tập riêng của bé đã được hệ thống ghi nhận. GVCN sẽ kiểm tra và phản hồi sớm nhất ạ.";
                    phanHoi.TrangThai = "Chờ GV xử lý";
                }
            }
               else if (keHoachGoc.LoaiThongBao != null && keHoachGoc.LoaiThongBao.Trim().Equals("Báo kế hoạch", StringComparison.OrdinalIgnoreCase))
                {
                    if (noiDungThuong.Contains("nhất trí") || noiDungThuong.Contains("đồng ý"))
                    {
                        phanHoiCuaHeThong = "Trợ lý ảo: Đã ghi nhận sự đồng ý của phụ huynh.";
                        phanHoi.TrangThai = "AI đã trả lời";
                    }
                    else
                    {
                        phanHoiCuaHeThong = "Trợ lý ảo: Đã ghi nhận ý kiến, GV sẽ xem xét.";
                        phanHoi.TrangThai = "Chờ GV xử lý";
                    }
                }

            // 3. Cập nhật trạng thái thông báo gốc
            var thongBaoGoc = await _context.TuongTacs
                .FirstOrDefaultAsync(t => t.MaKeHoach == phanHoi.MaKeHoach
                                      && t.TenDangNhap == phanHoi.TenDangNhap
                                      && t.TrangThai == "Chưa xem");

            if (thongBaoGoc != null)
            {
                thongBaoGoc.TrangThai = "Đã phản hồi";
            }

            // 3.1 Lưu tin nhắn của phụ huynh vào DB
            // 3.1 Lưu tin nhắn của phụ huynh vào DB
            phanHoi.ThoiGian = DateTime.Now;
            _context.TuongTacs.Add(phanHoi);

            // 3.2 LƯU CÂU TRẢ LỜI CỦA TRỢ LÝ ẢO VÀO DB
            // Chỉ cần AI có mở miệng nói (chuỗi không rỗng) là bắt buộc phải lưu!
            if (!string.IsNullOrEmpty(phanHoiCuaHeThong))
            {
                var tinNhanCuaAI = new TuongTac
                {
                    MaKeHoach = phanHoi.MaKeHoach,
                    TenDangNhap = phanHoi.TenDangNhap,
                    NoiDung = phanHoiCuaHeThong,
                    TrangThai = "Hệ thống trả lời",
                    ThoiGian = DateTime.Now.AddSeconds(2) // Cộng 2 giây để chắc chắn nó xếp sau câu hỏi
                };
                _context.TuongTacs.Add(tinNhanCuaAI);
            }

            // 3.3 QUAN TRỌNG NHẤT: Lệnh này phải nằm CUỐI CÙNG để lưu CẢ 2 dòng vào SQL
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Gửi thành công!",
                trangThai = phanHoi.TrangThai,
                noiDungPhanHoi = phanHoiCuaHeThong
            });
        }

        /// Cấp số liệu cho cái Chuông thông báo trên giao diện Front-end (Đã fix lỗi chỉ báo cho đúng GVCN)
        /// </summary>
        [HttpGet("thong-bao-chuong")]
        [Authorize] // Bắt buộc phải có token đăng nhập mới được gọi API này
        public async Task<IActionResult> DemSoTinNhanCho()
        {
            // 1. Lấy mã giáo viên đang đăng nhập từ Token (Chìa khóa)
            var maGiaoVien = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(maGiaoVien))
            {
                return Unauthorized(new { message = "Lỗi bảo mật: Không xác định được người dùng!" });
            }

            // 2. Chỉ đếm tin nhắn "Chờ GV xử lý" VÀ tin nhắn đó phải thuộc về cái Kế hoạch/Thông báo do chính giáo viên này đăng
            var soLuong = await _context.TuongTacs
                .Include(t => t.MaKeHoachNavigation) // Kết nối sang bảng KeHoachLop
                .CountAsync(t => t.TrangThai == "Chờ GV xử lý" &&
                                 t.MaKeHoachNavigation.NguoiDang == maGiaoVien); // Chốt chặn bảo mật ở đây!

            return Ok(new
            {
                soThongBaoChuaDoc = soLuong,
                message = soLuong > 0 ? $"Bạn có {soLuong} phản hồi cần xử lý" : "Không có thông báo mới"
            });
        }
        /// <summary>
        /// API để Phụ huynh xem danh sách thông báo và điểm số
        /// </summary>
        [HttpGet("hop-thu-ca-nhan/{tenDangNhap}")]
        public async Task<IActionResult> LayHopThuPhuHuynh(string tenDangNhap)
        {
            // Truy vấn bảng TuongTac, lọc đúng tài khoản đang đăng nhập và xếp tin mới nhất lên đầu
            var danhSachTinNhan = await _context.TuongTacs
                .Where(t => t.TenDangNhap == tenDangNhap)
                .OrderByDescending(t => t.ThoiGian)
                .ToListAsync();

            if (danhSachTinNhan.Count == 0)
            {
                return Ok(new { message = "Hộp thư của bạn hiện đang trống." });
            }
            // --- ĐOẠN CODE BỔ SUNG: TỰ ĐỘNG ĐÁNH DẤU ĐÃ XEM ---
            var tinNhanChuaXem = danhSachTinNhan.Where(t => t.TrangThai == "Chưa xem").ToList();
            if (tinNhanChuaXem.Any())
            {
                foreach (var tin in tinNhanChuaXem)
                {
                    tin.TrangThai = "Đã xem";
                }
                // Lưu sự thay đổi xuống SQL
                await _context.SaveChangesAsync();
            }
            // --------------------------------------------------

            return Ok(danhSachTinNhan);
            return Ok(danhSachTinNhan);
        }
    }
}