using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAn_WebHocVu_API.Models;
using Microsoft.AspNetCore.Http;
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
            IFormFile fileDinhKem)
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

            // 3. Thêm vào bảng Kế Hoạch và lưu Database
            _context.KeHoachLops.Add(keHoach);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã đăng kế hoạch thành công!", data = keHoach });
        }
    }
}