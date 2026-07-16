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

            string mndTrim = maGiaoVien?.Trim().ToUpper() ?? "";
            string mmTrim = model.MaMon?.Trim().ToUpper() ?? "";

            // LUẬT BẤT BẠI: Quét đồng thời 2 tư cách (Không quan tâm chữ Cơ bản hay Chuyên nữa)

            // 1) Tư cách GVCN:
            var lopHoc = await _context.LopHocs.FirstOrDefaultAsync(l => l.MaLop == hocSinh.MaLop);
            bool laGVCN = (lopHoc != null && lopHoc.GvchuNhiem?.Trim().ToUpper() == mndTrim);

            // 2) Tư cách GVBM (Có trên bảng Phân công):
            bool laGVBM = await _context.PhanCongGiangDays
                .AnyAsync(pc => pc.MaGiaoVien.Trim().ToUpper() == mndTrim && pc.MaLop == hocSinh.MaLop && pc.MaMon.Trim().ToUpper() == mmTrim);

            if (laGVBM)
            {
                // Chân lý tuyệt đối: Có phân công là được nhập điểm môn đó, bất kể loại môn gì!
                duocPhepThaoTac = true; 
            }
            else if (laGVCN)
            {
                // Nếu chưa được phân công nhưng là GVCN -> Cho phép nhập điểm các môn đại trà,
                // NGOẠI TRỪ môn đó đã được nhà trường giăng sẵn một Giáo viên chuyên trách nẫng tay trên!
                bool daCoGvChuyenTrach = await _context.PhanCongGiangDays
                    .AnyAsync(pc => pc.MaLop == hocSinh.MaLop && pc.MaMon.Trim().ToUpper() == mmTrim);
                
                if (daCoGvChuyenTrach)
                {
                    return StatusCode(403, new { message = $"Từ chối truy cập: Bạn là Chủ nhiệm, nhưng môn {monHoc.TenMon} của lớp này đã được giao phó riêng cho Giáo viên bộ môn. Bạn không được nhập đè!" });
                }

                duocPhepThaoTac = true;
            }

            // 4. Phán quyết cuối cùng về quyền hạn
            if (!duocPhepThaoTac)
            {
                return StatusCode(403, new { message = "Lỗi phân quyền: Bạn không có quyền nhập điểm cho môn này của lớp này!" });
            }

            // 5. NGHIỆP VỤ PHÂN LOẠI MÔN THEO THÔNG TƯ 27
            var cacMonNhanXet = new List<string> { "TNXH", "GDTC", "HDTN", "HĐTN", "DD", "AN", "MT" };
            
            // Cảm biến siêu việt: Tiếng Anh Khối 1, 2 là môn làm quen (nhận xét chữ), Khối 3 trở lên mới tính điểm
            bool hocKhoi12 = hocSinh.MaLop.Contains("1") || hocSinh.MaLop.Contains("2");
            if (hocKhoi12)
            {
                cacMonNhanXet.Add("ANH");
            }

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
                    XepLoai = b.XepLoai,
                    NhanXet = b.NhanXet,
                    NgayCapNhat = b.NgayCapNhat
                })
                .ToListAsync();

            if (!bangDiem.Any())
                return Ok(new List<object>()); // Return empty list rather than 404 to avoid frontend console error spikes

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

            // Rào chắn: Hiệu trưởng được xem tất cả, Giáo viên phải là chủ nhiệm
            if (!User.IsInRole("HieuTruong"))
            {
                if (lopHoc.GvchuNhiem?.Trim().ToUpper() != maNguoiDung?.Trim().ToUpper())
                {
                    return StatusCode(403, new { message = $"TỪ CHỐI: Chỉ Giáo viên chủ nhiệm mới được quyền xuất điểm của lớp {lopHoc.TenLop}." });
                }
            }
            // BƯỚC 2: CHUẨN BỊ DỮ LIỆU
            var danhSachHocSinh = await _context.HocSinhs.Where(h => h.MaLop == maLop).ToListAsync();
            var maHocSinhs = danhSachHocSinh.Select(h => h.MaHs).ToList();
            var danhSachDiem = await _context.BangDiems.Where(b => maHocSinhs.Contains(b.MaHs)).ToListAsync();

            // --- THUẬT TOÁN GỘP MÔN HỌC THÀNH VIÊN THEO KHỐI LỚP ---
            var tatCaMon = await _context.MonHocs.ToListAsync();
            var validSubjectCodes = GetClassSubjectCodes(maLop);
            var danhSachMonHoc = tatCaMon
                .Where(m => validSubjectCodes.Contains(m.MaMon.Trim().ToUpper()))
                .ToList();

            // DANH SÁCH MIỄN TRỪ ĐIỂM SỐ (Chỉ kiểm tra Xếp loại) - Đồng bộ theo Mã Môn
            var cacMonNgoaiLeCodes = new List<string> { "DD", "GDTC", "AN", "MT", "HDTN", "HĐTN", "TNXH" };
            if (maLop.Contains("1") || maLop.Contains("2"))
            {
                cacMonNgoaiLeCodes.Add("ANH");
            }
            var danhSachLoi = new List<string>();

            // BƯỚC 3: THUẬT TOÁN QUÉT LỖ HỔNG DỮ LIỆU
            foreach (var hs in danhSachHocSinh)
            {
                // Thẻ bài miễn trừ: Học sinh chuyển trường thì không cần quét điểm
                if (hs.TrangThai == "Đã chuyển trường") continue;

                foreach (var mon in danhSachMonHoc)
                {
                    var diemMon = danhSachDiem.FirstOrDefault(d => d.MaHs == hs.MaHs && d.MaMon == mon.MaMon);
                    bool laMonNhanXet = cacMonNgoaiLeCodes.Contains(mon.MaMon.Trim().ToUpper());

                    if (laMonNhanXet)
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
            var ketQuaXuat = danhSachHocSinh.Where(hs => hs.TrangThai != "Đã chuyển trường").Select(hs =>
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
                        LaMonNhanXet = cacMonNgoaiLeCodes.Contains(mon.MaMon.Trim().ToUpper())
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

            var validSubjectCodes = GetClassSubjectCodes(maLop);
            var danhSachMonHoc = tatCaMon
                .Where(m => validSubjectCodes.Contains(m.MaMon.Trim().ToUpper()))
                .ToList();

            var cacMonNgoaiLeCodes = new List<string> { "DD", "GDTC", "AN", "MT", "HDTN", "HĐTN", "TNXH" };
            if (maLop.Contains("1") || maLop.Contains("2"))
            {
                cacMonNgoaiLeCodes.Add("ANH");
            }

            // --- BƯỚC 2: QUÉT LỖ HỔNG (CHỐT CHẶN BẮT BUỘC) ---
            var danhSachLoi = new List<string>();
            foreach (var hs in danhSachHocSinh)
            {
                if (hs.TrangThai == "Đã chuyển trường") continue;

                foreach (var mon in danhSachMonHoc)
                {
                    var diemCuaHs = danhSachDiem.FirstOrDefault(d => d.MaHs == hs.MaHs && d.MaMon == mon.MaMon);
                    bool laMonNhanXet = cacMonNgoaiLeCodes.Contains(mon.MaMon.Trim().ToUpper());

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
                    string diemHienThi = "";
                    if (d != null)
                    {
                        if (d.DiemThi != null && !string.IsNullOrEmpty(d.XepLoai))
                        {
                            diemHienThi = $"{d.DiemThi} ({d.XepLoai})"; // Môn có cả điểm và chữ: "6 (H)"
                        }
                        else if (d.DiemThi != null)
                        {
                            diemHienThi = d.DiemThi.ToString(); // Chỉ có điểm
                        }
                        else if (!string.IsNullOrEmpty(d.XepLoai))
                        {
                            diemHienThi = d.XepLoai; // Chỉ có chữ: "H"
                        }
                    }
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
        private List<string> GetClassSubjectCodes(string maLop)
        {
            if (string.IsNullOrEmpty(maLop)) return new List<string>();

            bool laKhoi12 = maLop.Contains("1") || maLop.Contains("2");
            bool laKhoi3 = maLop.Contains("3");

            var list = new List<string> { "TOAN", "TV", "DD", "GDTC", "AN", "MT", "HDTN", "HĐTN" };

            if (laKhoi12)
            {
                list.Add("TNXH");
                list.Add("ANH");
            }
            else if (laKhoi3)
            {
                list.Add("TNXH");
                list.Add("ANH");
                list.Add("TIN");
                list.Add("CN");
            }
            else
            {
                list.Add("KH");
                list.Add("LSĐL");
                list.Add("ANH");
                list.Add("TIN");
                list.Add("CN");
            }

            return list;
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