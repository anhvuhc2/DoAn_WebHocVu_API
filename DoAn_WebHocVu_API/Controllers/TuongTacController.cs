using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAn_WebHocVu_API.Models;

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
        [HttpPost("nhan-phan-hoi")]
        public async Task<IActionResult> NhanPhanHoiTuPhuHuynh([FromBody] TuongTac phanHoi)
        {
            // 1. Lưu tin nhắn của phụ huynh vào Database
            phanHoi.ThoiGian = DateTime.Now;
            phanHoi.TrangThai = "Đang phân tích...";

            _context.TuongTacs.Add(phanHoi);
            await _context.SaveChangesAsync();

            // =======================================================
            // 2. KÍCH HOẠT TRỢ LÝ ẢO (TÁC VỤ 4 CHUẨN BỊ LẮP VÀO ĐÂY)
            // =======================================================

            bool laCauHoiChung = phanHoi.NoiDung.Contains("xếp loại") || phanHoi.NoiDung.Contains("điểm H");
            string phanHoiCuaHeThong = "";

            if (laCauHoiChung)
            {
                // Giả lập AI trả lời
                phanHoiCuaHeThong = "Trợ lý ảo học vụ: Dạ thưa phụ huynh, theo thông tư 27, điểm H có nghĩa là học sinh 'Hoàn thành' mục tiêu môn học ạ.";
                phanHoi.TrangThai = "AI đã trả lời";
            }
            else
            {
                // Câu hỏi khó -> Đẩy cho GVCN
                phanHoiCuaHeThong = "Trợ lý ảo học vụ: Dạ thắc mắc của anh/chị về tình hình riêng của bé đã được hệ thống ghi nhận. GVCN sẽ kiểm tra sổ điểm và phản hồi sớm nhất ạ.";
                phanHoi.TrangThai = "Chờ GV xử lý"; // Trạng thái này sẽ làm nổi chuông thông báo bên app của GVCN
            }

            // Cập nhật lại trạng thái vào Database
            _context.TuongTacs.Update(phanHoi);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Đã tiếp nhận phản hồi!",
                phanHoiTuDong = phanHoiCuaHeThong,
                trangThai = phanHoi.TrangThai
            });
        }
        /// <summary>
        /// Cấp số liệu cho cái Chuông thông báo trên giao diện Front-end
        /// </summary>
        [HttpGet("thong-bao-chuong")]
        public async Task<IActionResult> DemSoTinNhanCho()
        {
            // Đếm tất cả những tin nhắn có trạng thái là "Chờ GV xử lý"
            var soLuong = await _context.TuongTacs.CountAsync(t => t.TrangThai == "Chờ GV xử lý");

            return Ok(new
            {
                soThongBaoChuaDoc = soLuong,
                message = soLuong > 0 ? $"Bạn có {soLuong} phản hồi cần xử lý" : "Không có thông báo mới"
            });
        }
    }
}