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

        // ====================================================================
        // CHỨC NĂNG: NHẬP ĐIỂM / XẾP LOẠI LINH HOẠT THEO TIỂU HỌC (ĐÃ KIỂM TRA PHÂN QUYỀN)
        // ====================================================================
        [HttpPost("nhap-diem")]
        public async Task<IActionResult> NhapDiem([FromBody] NhapDiemDto model)
        {
            // 1. Lấy mã giáo viên đang đăng nhập từ Token
            var maGiaoVien = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // 2. Lấy thông tin để đối chiếu
            var monHoc = await _context.MonHocs.FirstOrDefaultAsync(m => m.MaMon == model.MaMon);
            var hocSinh = await _context.HocSinhs.FirstOrDefaultAsync(h => h.MaHs == model.MaHS && h.TrangThai == "Đang học");

            if (monHoc == null)
            {
                return NotFound("Không tìm thấy môn học.");
            }
            if (hocSinh == null)
            {
                return BadRequest("Học sinh này không tồn tại hoặc đã chuyển trường/nghỉ học!");
            }

            // 3. THUẬT TOÁN GÁC CỔNG VỒNG TRONG (Kiểm tra chéo quyền của GV)
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
                    .AnyAsync(pc => pc.MaGiaoVien == maGiaoVien && pc.MaLop == hocSinh.MaLop && pc.MaMon == model.MaMon);

                if (duocPhanCong)
                {
                    duocPhepThaoTac = true; // Khớp phân công bộ môn -> Mở cổng
                }
            }

            // 4. Phán quyết cuối cùng về quyền hạn
            if (!duocPhepThaoTac)
            {
                return StatusCode(403, new { message = "Lỗi phân quyền: Bạn không có quyền nhập điểm cho môn này của lớp này!" });
            }

            // 5. NGHIỆP VỤ PHÂN LOẠI MÔN THEO THÔNG TƯ 27
            var cacMonNhanXet = new List<string> { "TNXH", "GDTC", "HDTN", "HĐTN", "DD" };

            if (cacMonNhanXet.Contains(model.MaMon.ToUpper()))
            {
                // Nếu là môn nhận xét (lớp 1,2,3) -> Bắt buộc cột điểm phải trống (NULL)
                model.DiemThi = null;
                if (string.IsNullOrEmpty(model.XepLoai))
                {
                    return BadRequest(new { message = $"Môn {model.MaMon} là môn đánh giá nhận xét, bắt buộc phải nhập xếp loại (H/T/C)!" });
                }
            }
            else
            {
                // Nếu là môn tính điểm (Toán, TV...) -> Bắt buộc phải điền điểm số
                if (model.DiemThi == null)
                {
                    return BadRequest(new { message = $"Môn {model.MaMon} yêu cầu phải có điểm số, không được bỏ trống!" });
                }
            }

            // 6. Tiến hành kiểm tra và lưu vết vào bảng dữ liệu
            var bangDiem = await _context.BangDiems.FirstOrDefaultAsync(b => b.MaHs == model.MaHS && b.MaMon == model.MaMon);

            if (bangDiem == null)
            {
                // Nếu chưa có điểm -> Tạo dòng mới (Thêm)
                bangDiem = new DoAn_WebHocVu_API.Models.BangDiem
                {
                    MaHs = model.MaHS,
                    MaMon = model.MaMon,
                    DiemThi = model.DiemThi,
                    XepLoai = model.XepLoai?.ToUpper(),
                    NhanXet = model.NhanXet,
                    NgayCapNhat = DateTime.Now
                };
                _context.BangDiems.Add(bangDiem);
            }
            else
            {
                // Nếu đã có điểm rồi -> Ghi đè dữ liệu cũ (Sửa)
                bangDiem.DiemThi = model.DiemThi;
                bangDiem.XepLoai = model.XepLoai?.ToUpper();
                bangDiem.NhanXet = model.NhanXet;
                bangDiem.NgayCapNhat = DateTime.Now;
                _context.BangDiems.Update(bangDiem);
            }

            // 7. Lưu vào Database
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Thành công! Đã cập nhật dữ liệu học tập môn {monHoc.TenMon} cho học sinh {hocSinh.HoTen}." });
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
        [Authorize(Roles = "GiaoVien")]
        public async Task<IActionResult> XuatBangDiemTong(string maLop)
        {
            // BƯỚC 1: LẤY THÔNG TIN VÀ KIỂM TRA QUYỀN
            var maNguoiDung = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == maLop);

            if (lopHoc == null) return NotFound(new { message = "Không tìm thấy lớp học này!" });

            // Rào chắn tuyệt đối: Chỉ cho phép đúng GVCN của lớp này
            if (lopHoc.GvchuNhiem?.Trim().ToUpper() != maNguoiDung?.Trim().ToUpper())
            {
                return StatusCode(403, new { message = $"TỪ CHỐI: Chỉ Giáo viên chủ nhiệm mới được quyền xuất điểm của lớp {lopHoc.TenLop}." });
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
            var danhSachMonHoc = tatCaMon.Where(m =>
            {
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
            var ketQuaXuat = danhSachHocSinh.Select(hs =>
            {

                // 1. Gom đầy đủ điểm và xếp loại
                var chiTietDiemHs = danhSachMonHoc.Select(mon =>
                {
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
                    ChiTietDiem = chiTietDiemHs.Select(d => new
                    {
                        d.TenMon,
                        d.DiemThi,
                        d.XepLoai,
                        d.NhanXet
                    }).ToList()
                };
            }).ToList();

            return Ok(new { message = "Dữ liệu đầy đủ hợp lệ!", data = ketQuaXuat });
        }
        /// <summary>
        /// <summary>
        /// API: Gửi thông báo điểm cho phụ huynh qua Zalo / SMS (Đã chốt chặn logic quy trình)
        /// </summary>
        [HttpPost("gui-thong-bao-diem/{maLop}")]
        [Authorize(Roles = "GiaoVien")]
        public async Task<IActionResult> GuiThongBaoDiem(string maLop)
        {
            var maNguoiDung = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == maLop);

            if (lopHoc == null) return NotFound(new { message = "Không tìm thấy lớp học này!" });

            // Rào chắn tuyệt đối: Chỉ cho phép đúng GVCN của lớp này
            if (lopHoc.GvchuNhiem?.Trim().ToUpper() != maNguoiDung?.Trim().ToUpper())
            {
                return StatusCode(403, new { message = $"TỪ CHỐI: Chỉ Giáo viên chủ nhiệm mới được quyền gửi thông báo cho lớp {lopHoc.TenLop}." });
            }

            // --- BƯỚC 1: LỌC MÔN HỌC THEO KHỐI LỚP (Giống API Xuất điểm) ---
            var danhSachHocSinh = await _context.HocSinhs.Where(h => h.MaLop == maLop).ToListAsync();
            var maHocSinhs = danhSachHocSinh.Select(h => h.MaHs).ToList();
            var danhSachDiem = await _context.BangDiems.Where(b => maHocSinhs.Contains(b.MaHs)).ToListAsync();
            var tatCaMon = await _context.MonHocs.ToListAsync();

            bool laKhoi123 = maLop.Contains("1") || maLop.Contains("2") || maLop.Contains("3");
            var maMonChuyen = await _context.PhanCongGiangDays.Where(pc => pc.MaLop == maLop).Select(pc => pc.MaMon).ToListAsync();

            var danhSachMonHoc = tatCaMon.Where(m =>
            {
                var ten = m.TenMon?.Trim().ToLower() ?? "";
                if (maMonChuyen.Contains(m.MaMon)) return true;
                if (ten.Contains("toán") || ten.Contains("tiếng việt") || ten.Contains("đạo đức") || ten.Contains("trải nghiệm")) return true;
                if (laKhoi123 && ten.Contains("tự nhiên và xã hội")) return true;
                if (!laKhoi123 && (ten.Contains("khoa học") || ten.Contains("lịch sử") || ten.Contains("địa lí"))) return true;
                return false;
            }).ToList();

            var danhSachNgoaiLe = new List<string> { "âm nhạc", "thể dục", "mĩ thuật", "hoạt động trải nghiệm", "đạo đức", "tự nhiên và xã hội" };

            // --- BƯỚC 2: QUÉT LỖ HỔNG (CHỐT CHẶN BẮT BUỘC) ---
            var danhSachLoi = new List<string>();
            foreach (var hs in danhSachHocSinh)
            {
                if (hs.TrangThai == "Đã chuyển trường") continue;

                foreach (var mon in danhSachMonHoc)
                {
                    var diemCuaHs = danhSachDiem.FirstOrDefault(d => d.MaHs == hs.MaHs && d.MaMon == mon.MaMon);
                    bool laMonNhanXet = danhSachNgoaiLe.Contains(mon.TenMon?.Trim().ToLower() ?? "");

                    if (diemCuaHs == null ||
                       (laMonNhanXet && string.IsNullOrEmpty(diemCuaHs.XepLoai)) ||
                       (!laMonNhanXet && diemCuaHs.DiemThi == null))
                    {
                        danhSachLoi.Add($"Em {hs.HoTen} bị trống điểm môn {mon.TenMon}");
                    }
                }
            }
            // NẾU CÓ LỖI CHƯA NHẬP ĐỦ -> CHẶN ĐỨNG QUY TRÌNH GỬI TIN NHẮN
            if (danhSachLoi.Count > 0)
            {
                return BadRequest(new
                {
                    message = "CHƯA THỂ GỬI THÔNG BÁO! Bảng điểm của lớp chưa được nhập hoàn tất.",
                    chiTietLoi = danhSachLoi
                });
            }
            // --- BƯỚC 3: TẠO THÔNG BÁO TỔNG CHO HIỆU TRƯỞNG (BẢNG KEHOACHLOP) ---
            // Tự động sinh ra một Mã Kế Hoạch duy nhất (ví dụ: BD_L01_20260621213000)
    
            var keHoachBaoDiem = new DoAn_WebHocVu_API.Models.KeHoachLop
            {
                
                MaLop = maLop,
                TieuDe = $"Báo điểm định kỳ lớp {lopHoc.TenLop}",
                NoiDung = $"Hệ thống đã gửi tự động bảng điểm chi tiết đến từng phụ huynh của lớp {lopHoc.TenLop}. Kính mong ban giám hiệu theo dõi tiến độ.",
                LoaiThongBao = "Báo điểm", // Khớp 100% với chữ trong CHECK CONSTRAINT dưới SQL
                NgayDang = DateTime.Now,
                NguoiDang = maNguoiDung // Mã của GVCN đang đăng nhập
            };
            // Thêm vào context chuẩn bị lưu
            _context.KeHoachLops.Add(keHoachBaoDiem);
            await _context.SaveChangesAsync();
            // --- BƯỚC 4: TỰ ĐỘNG LƯU THÔNG BÁO CÁ NHÂN VÀO BẢNG TUONGTAC ---
            foreach (var hs in danhSachHocSinh)
            {
                if (hs.TrangThai == "Đã chuyển trường") continue;
                var chiTietDiem = new List<string>();
                foreach (var mon in danhSachMonHoc)
                {
                    var d = danhSachDiem.FirstOrDefault(x => x.MaHs == hs.MaHs && x.MaMon == mon.MaMon);
                    string diemHienThi = d?.DiemThi != null ? d?.DiemThi.ToString() : d?.XepLoai ?? "";
                    chiTietDiem.Add($"{mon.TenMon}: {diemHienThi}");
                }
                string noiDungTinNhan = $"Trường TH thông báo điểm của em {hs.HoTen}: {string.Join(", ", chiTietDiem)}.";
                string maTaiKhoanPhuHuynh = ("PH_" + hs.MaHs.Trim()).Trim();
                var taiKhoanPhuHuynh = await _context.TaiKhoans
                    .FirstOrDefaultAsync(tk => tk.TenDangNhap == maTaiKhoanPhuHuynh);

                string? tkHienTai = taiKhoanPhuHuynh != null ? taiKhoanPhuHuynh.TenDangNhap : null;
                if (tkHienTai != null)
                {
                    var tuongTacMoi = new DoAn_WebHocVu_API.Models.TuongTac
                    {
                        MaKeHoach = keHoachBaoDiem.MaKeHoach,
                        TenDangNhap = tkHienTai, // Đã có dấu phẩy ngăn cách chuẩn xác
                        NoiDung = $"[Thông báo cá nhân] - {noiDungTinNhan}",
                        ThoiGian = DateTime.Now,
                        TrangThai = "Chưa xem"
                    };

                    // Đưa dữ liệu vào Context chuẩn bị lưu
                    _context.TuongTacs.Add(tuongTacMoi);
                }

                // Lưu TẤT CẢ thay đổi (bao gồm cả KeHoachLop và hàng loạt TuongTac) xuống Database cùng một lúc
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = $"Đã gửi thông báo điểm thành công cho lớp {lopHoc.TenLop}. Ban giám hiệu có thể theo dõi tại mục Kế hoạch lớp!" });
        }
        public class NhapDiemDto
        {
            public string? MaHS { get; set; }
            public string? MaMon { get; set; }
            public float? DiemThi { get; set; } // Dấu ? cho phép truyền null từ Swagger lên
            public string? XepLoai { get; set; }
            public string? NhanXet { get; set; }
        }
    }
}