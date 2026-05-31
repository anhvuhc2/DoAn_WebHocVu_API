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
    [Authorize(Roles = "GiaoVien,HieuTruong")] // CHỐT CHẶN VÒNG NGOÀI: Phải có thẻ Giáo Viên mới được gọi API này
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
            var hocSinh = await _context.HocSinhs.FirstOrDefaultAsync(h => h.MaHs == maHS && h.TrangThai == "Đang học");

            if (monHoc == null)
            {
                return NotFound("Không tìm thấy môn học.");
            }
            if (hocSinh == null)
            {
                return BadRequest("Học sinh này không tồn tại hoặc đã chuyển trường/nghỉ học!");
            }

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
    
    /// <summary>
        /// API: Xuất Bảng Điểm Tổng (Chỉ GVCN mới được xuất)
        /// </summary>
        [HttpGet("xuat-bang-diem-tong/{maLop}")]
        [Authorize(Roles = "GiaoVien,HieuTruong")]
        public async Task<IActionResult> XuatBangDiemTong(string maLop)
        {
            // BƯỚC 1: LẤY THÔNG TIN VÀ KIỂM TRA QUYỀN
            var maNguoiDung = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == maLop);

            if (lopHoc == null) return NotFound(new { message = "Không tìm thấy lớp học này!" });

            // Nếu không phải hiệu trưởng thì bắt buộc phải là GVCN của lớp này
            var vaiTro = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (vaiTro != "HieuTruong" && lopHoc.GvchuNhiem?.Trim().ToUpper() != maNguoiDung?.Trim().ToUpper())
            {
                return StatusCode(403, new { message = $"TỪ CHỐI: Chỉ Giáo viên chủ nhiệm mới được quyền xuất điểm tổng của lớp {lopHoc.TenLop}." });
            }

            // BƯỚC 2: CHUẨN BỊ DỮ LIỆU
            var danhSachHocSinh = await _context.HocSinhs.Where(h => h.MaLop == maLop).ToListAsync();
            var maHocSinhs = danhSachHocSinh.Select(h => h.MaHs).ToList();
            var danhSachDiem = await _context.BangDiems.Where(b => maHocSinhs.Contains(b.MaHs)).ToListAsync();

            // --- THUẬT TOÁN GỘP MÔN HỌC THÔNG MINH ---
            // 1. Lấy toàn bộ môn học trong trường
            var tatCaMon = await _context.MonHocs.ToListAsync();

            // 2. Xác định xem lớp này là khối 1, 2, 3 hay khối 4, 5 dựa vào ký tự trong mã lớp
            bool laKhoi123 = maLop.Contains("1") || maLop.Contains("2") || maLop.Contains("3");

            // 3. Vào sổ phân công lấy danh sách các MÔN CHUYÊN (Tiếng Anh, Thể dục...)
            var maMonChuyen = await _context.PhanCongGiangDays
                                            .Where(pc => pc.MaLop == maLop)
                                            .Select(pc => pc.MaMon)
                                            .ToListAsync();

            // 4. BỘ LỌC TỰ ĐỘNG CHỌN MÔN (Kết hợp Đại trà + Chuyên)
            var danhSachMonHoc = tatCaMon.Where(m => {
                var ten = m.TenMon?.Trim().ToLower() ?? "";

                // Tiêu chí 1: Nếu là môn chuyên đã được phân công -> Lấy!
                if (maMonChuyen.Contains(m.MaMon)) return true;

                // Tiêu chí 2: Các môn đại trà chung bắt buộc phải có ở mọi khối
                if (ten.Contains("toán") || ten.Contains("tiếng việt") || ten.Contains("đạo đức") || ten.Contains("trải nghiệm")) return true;

                // Tiêu chí 3: Phân loại đại trà theo khối
                if (laKhoi123 && ten.Contains("tự nhiên và xã hội")) return true;
                if (!laKhoi123 && (ten.Contains("khoa học") || ten.Contains("lịch sử") || ten.Contains("địa lí"))) return true;

                return false; // Nếu không thuộc các nhóm trên thì bỏ qua
            }).ToList();
            // ------------------------------------------

            // DANH SÁCH MIỄN TRỪ ĐIỂM SỐ (Chỉ kiểm tra Xếp loại)
            var danhSachNgoaiLe = new List<string> { "âm nhạc", "thể dục", "mĩ thuật", "hoạt động trải nghiệm", "đạo đức", "tự nhiên và xã hội" };
            var danhSachLoi = new List<string>();

            // BƯỚC 3: THUẬT TOÁN QUÉT LỖ HỔNG DỮ LIỆU
            foreach (var hs in danhSachHocSinh)
            {
                // Thẻ bài miễn trừ: Học sinh chuyển trường thì không cần quét điểm
                if (hs.TrangThai == "Đã chuyển trường") continue;

                foreach (var mon in danhSachMonHoc)
                {
                    var diemMon = danhSachDiem.FirstOrDefault(d => d.MaHs == hs.MaHs && d.MaMon == mon.MaMon);
                    var tenMonClean = mon.TenMon?.Trim().ToLower() ?? "";

                    if (danhSachNgoaiLe.Contains(tenMonClean))
                    {
                        // LUẬT 1: Môn đánh giá -> Kiểm tra cột XepLoai
                        if (diemMon == null || string.IsNullOrWhiteSpace(diemMon.XepLoai))
                        {
                            danhSachLoi.Add($"Em {hs.HoTen} chưa có đánh giá (Xếp loại) môn {mon.TenMon}.");
                        }
                    }
                    else
                    {
                        // LUẬT 2: Môn cho điểm -> Bắt buộc phải có DiemThi
                        if (diemMon == null || diemMon.DiemThi == null)
                        {
                            danhSachLoi.Add($"Em {hs.HoTen} đang bị trống điểm thi môn {mon.TenMon}.");
                        }
                    }
                }
            }

            // BƯỚC 4: CHỐT KẾT QUẢ
            if (danhSachLoi.Count > 0)
            {
                // Phát hiện thiếu sót -> Chặn lại và báo danh sách lỗi chi tiết
                return BadRequest(new
                {
                    message = "CHƯA THỂ XUẤT! Hệ thống phát hiện dữ liệu chưa hoàn tất.",
                    chiTietLoi = danhSachLoi
                });
            }

            // Nếu dữ liệu sạch 100%, tiến hành gom dữ liệu xuất ra
            var ketQuaXuat = danhSachHocSinh.Select(hs => {

                // 1. Gom đầy đủ điểm và xếp loại
                var chiTietDiemHs = danhSachMonHoc.Select(mon => {
                    var d = danhSachDiem.FirstOrDefault(x => x.MaHs == hs.MaHs && x.MaMon == mon.MaMon);
                    return new
                    {
                        TenMon = mon.TenMon,
                        DiemThi = d?.DiemThi,
                        XepLoai = d?.XepLoai,
                        NhanXet = d?.NhanXet,
                        LaMonNhanXet = danhSachNgoaiLe.Contains(mon.TenMon?.Trim().ToLower() ?? "")
                    };
                }).ToList();

                // 2. THUẬT TOÁN XÉT KHEN THƯỞNG TỰ ĐỘNG
                string khenThuong = "";
                var cacMonNhanXet = chiTietDiemHs.Where(x => x.LaMonNhanXet).ToList();

                // Tất cả môn nhận xét phải là T
                bool tatCaMonNhanXetDatT = cacMonNhanXet.All(x => x.XepLoai?.Trim().ToUpper() == "T");

                // Chỉ xét khen thưởng nếu các môn nhận xét đều đạt loại T
                if (tatCaMonNhanXetDatT && cacMonNhanXet.Count > 0)
                {
                    var cacMonTinhDiem = chiTietDiemHs.Where(x => !x.LaMonNhanXet).ToList();

                    // Trường hợp 1: Tất cả môn tính điểm đều >= 9
                    bool tatCaMonTu9TroLen = cacMonTinhDiem.All(x => x.DiemThi >= 9);

                    if (tatCaMonTu9TroLen && cacMonTinhDiem.Count > 0)
                    {
                        khenThuong = "Học sinh xuất sắc";
                    }
                    else
                    {
                        // Trường hợp 2: Có môn >= 9 và các môn còn lại >= 7
                        var cacMonTu9 = cacMonTinhDiem.Where(x => x.DiemThi >= 9).ToList();
                        var cacMonConLai = cacMonTinhDiem.Where(x => x.DiemThi < 9).ToList();

                        bool cacMonConLaiDatKhá = cacMonConLai.All(x => x.DiemThi >= 7);

                        if (cacMonTu9.Count > 0 && cacMonConLaiDatKhá)
                        {
                            var tenMonTieuBieu = string.Join(", ", cacMonTu9.Select(x => x.TenMon));
                            khenThuong = $"Học sinh tiêu biểu môn {tenMonTieuBieu}";
                        }
                    }
                }

                // 3. Trả về cấu trúc có cột KhenThuong
                return new
                {
                    MaHs = hs.MaHs,
                    HoTen = hs.HoTen,
                    TrangThai = hs.TrangThai,
                    KhenThuong = khenThuong,
                    ChiTietDiem = chiTietDiemHs.Select(d => new {
                        d.TenMon,
                        d.DiemThi,
                        d.XepLoai,
                        d.NhanXet
                    }).ToList()
                };
            }).ToList();

            return Ok(new { message = "Dữ liệu đầy đủ hợp lệ!", data = ketQuaXuat });
        }
    }
}