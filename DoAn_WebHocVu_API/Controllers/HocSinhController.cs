using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoAn_WebHocVu_API.Models;

namespace DoAn_WebHocVu_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HocSinhController : ControllerBase
    {
        private readonly DoAnWebHocVuAdvancedContext _context;

        // "Bơm" kết nối Database vào đây
        public HocSinhController(DoAnWebHocVuAdvancedContext context)
        {
            _context = context;
        }

        // API: Lấy toàn bộ danh sách học sinh
        // Link chạy thử: api/HocSinh
        [HttpGet]
        public async Task<IActionResult> GetDanhSachHocSinh()
        {
            // Lấy dữ liệu từ bảng HocSinh trong SQL
            var danhSach = await _context.HocSinhs.ToListAsync();
            return Ok(danhSach);
        }


        // API: Thêm mới một học sinh
        // Link chạy thử: POST api/HocSinh
        [HttpPost]
        public async Task<IActionResult> ThemHocSinh(HocSinh hs)
        {
            // Thêm học sinh mới vào bộ nhớ đệm
            _context.HocSinhs.Add(hs);

            // Lưu thực sự xuống SQL Server
            await _context.SaveChangesAsync();

            return Ok(new { message = "Thêm học sinh thành công!", data = hs });
        }
        // API: Cập nhật thông tin học sinh (Update)
        // Link chạy thử: PUT api/HocSinh/HS001
        [HttpPut("{maHs}")]
        public async Task<IActionResult> CapNhatHocSinh(string maHs, HocSinh hsDaSua)
        {
            // Tìm xem học sinh có mã này trong database không
            var hocSinh = await _context.HocSinhs.FindAsync(maHs);
            if (hocSinh == null)
            {
                return NotFound(new { message = "Không tìm thấy học sinh này để cập nhật!" });
            }

            // Tiến hành đè dữ liệu mới lên các cột tương ứng
            hocSinh.HoTen = hsDaSua.HoTen;
            hocSinh.NgaySinh = hsDaSua.NgaySinh;
            hocSinh.MaLop = hsDaSua.MaLop;
            hocSinh.TaiKhoanPhuHuynh = hsDaSua.TaiKhoanPhuHuynh;

            // Lưu thay đổi xuống SQL Server
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật thông tin học sinh thành công!", data = hocSinh });
        }

        // API: Xóa học sinh (Delete)
        // Link chạy thử: DELETE api/HocSinh/HS001
        [HttpDelete("{maHs}")]
        public async Task<IActionResult> XoaHocSinh(string maHs)
        {
            // Tìm học sinh cần xóa
            var hocSinh = await _context.HocSinhs.FindAsync(maHs);
            if (hocSinh == null)
            {
                return NotFound(new { message = "Không tìm thấy học sinh này để xóa!" });
            }

            // Ra lệnh xóa
            _context.HocSinhs.Remove(hocSinh);

            // Xác nhận lưu xuống database
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa học sinh thành công!" });
        }
    }
}