using DoAn_WebHocVu_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace DoAn_WebHocVu_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TuongTacController : ControllerBase
    {
        private readonly DoAnWebHocVuAdvancedContext _context;
        private readonly IConfiguration _config;

        public TuongTacController(DoAnWebHocVuAdvancedContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        /// <summary>
        /// Webhook: Hứng tin nhắn phản hồi từ Phụ huynh (Zalo/App)
        /// </summary>
        [HttpPost("gui-phan-hoi")]
        public async Task<IActionResult> PostPhanHoi([FromBody] PhanHoiDto dto)
        {
            if (dto == null) return BadRequest("Dữ liệu phản hồi không được để trống!");

            var phanHoi = new TuongTac
            {
                MaKeHoach = dto.MaKeHoach,
                TenDangNhap = dto.TenDangNhap,
                NoiDung = !string.IsNullOrEmpty(dto.MaGiaoVien) ? $"[TO:{dto.MaGiaoVien.Trim()}] {dto.NoiDung}" : dto.NoiDung,
                TrangThai = dto.TrangThai
            };

            // 1. Tìm kế hoạch gốc (chỉ áp dụng nếu có đính kèm Plan, nếu null thì bỏ qua đi vào luồng TH2 Direct Msg)
            KeHoachLop? keHoachGoc = null;
            if (phanHoi.MaKeHoach.HasValue)
            {
                keHoachGoc = await _context.KeHoachLops.FindAsync(phanHoi.MaKeHoach);
                if (keHoachGoc == null) return NotFound("Không tìm thấy kế hoạch này.");
            }

            // 2. Logic AI Tích hợp Gemini 1.5 Flash theo phân luồng context-aware (Báo điểm vs Kế hoạch)
            string noiDungGoc = dto.NoiDung ?? "";
            string phanHoiCuaHeThong = "";
            string loaiThongBao = "BaoDiem"; // Mặc định nếu không đính kèm Kế hoạch học tập

            if (keHoachGoc != null && !string.IsNullOrEmpty(keHoachGoc.LoaiThongBao))
            {
                loaiThongBao = keHoachGoc.LoaiThongBao;
            }

            string apiKey = _config["GeminiApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                phanHoiCuaHeThong = "Trợ lý ảo hệ thống: Tính năng Trí Tuệ Nhân Tạo đang tạm thời đóng băng vì Backend chưa khai báo Gemini API Key trong appsettings.json. Yêu cầu của phụ huynh đã được chuyển về hộp thư của GVCN.";
                phanHoi.TrangThai = "Chờ GV xử lý";
            }
            else
            {
                var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(20); // 20 seconds timeout to prevent hanging
                var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent?key={apiKey}";
                
                string systemInstruction = "";
                if (loaiThongBao == "KeHoach")
                {
                    systemInstruction = "Nhiệm vụ: Phân tích kỹ phản hồi của phụ huynh đối với một Kế hoạch/Thông báo lớp học (dã ngoại, thu phí, sự kiện...).\n" +
                        "1. Nếu phụ huynh biểu thị sự đồng ý, nhất trí, xác nhận đã nhận thông tin (ví dụ: 'tôi đồng ý', 'nhất trí', 'ok', 'đã nhận thông tin', 'nhà trường cứ tiến hành', v.v.): Bạn hãy phản hồi thật ngắn gọn, thân thiện giải thích rằng hệ thống đã ghi nhận sự đồng thuận của phụ huynh, đồng thời BẮT BUỘC chèn từ khóa [AGREE] vào cuối câu trả lời của bạn.\n" +
                        "2. ĐỐI VỚI CÁC CÂU HỎI THẮC MẮC CHI TIẾT, Ý KIẾN TRÁI CHIỀU HOẶC YÊU CẦU GIẢI QUYẾT (ví dụ: hỏi chi phí, xin lùi lịch, phản đối kế hoạch, đóng tiền trễ, v.v.): Bạn tuyệt đối KHÔNG ĐƯỢC trả lời gì khác, chỉ in ra duy nhất từ khóa: [ESCALATE]";
                }
                else
                {
                    systemInstruction = "Nhiệm vụ: Phân tích kỹ câu hỏi của phụ huynh liên quan đến kết quả học tập / học vụ / bảng điểm.\n" +
                        "1. Chỉ khi phụ huynh hỏi về xếp loại học tập, đánh giá học lực theo Thông tư 27 (như tốt, hoàn thành, chưa hoàn thành, cách xếp loại môn học...) hoặc các môn nhận xét (Đạo đức, Thể dục, Âm nhạc): Bạn hãy giải thích trực tiếp thật ngắn gọn, lịch sự, sư phạm và BẮT BUỘC chèn link tài liệu `[Tài Liệu Mở Rộng: /files/ThongTu27.pdf]` ở dòng cuối cùng.\n" +
                        "2. ĐỐI VỚI TẤT CẢ MỌI CÂU HỎI KHÔNG LIÊN QUAN ĐẾN THÔNG TƯ 27 (ví dụ: đóng học phí, chi phí, xin nghỉ học, học sinh đánh nhau, tình trạng sinh hoạt học tập chung của con, v.v.): Bạn tuyệt đối KHÔNG ĐƯỢC trả lời gì khác, chỉ in ra duy nhất từ khóa: [ESCALATE]";
                }
                
                object payload = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = $"{systemInstruction}\n\nTin nhắn của phụ huynh: {noiDungGoc}" } } }
                    }
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                try
                {
                    var response = await httpClient.PostAsync(requestUri, content);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var jsonDoc = JsonDocument.Parse(responseBody);
                    string responseText = "";

                    if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidatesArr) && 
                        candidatesArr.GetArrayLength() > 0 &&
                        candidatesArr[0].TryGetProperty("content", out var contentObj) &&
                        contentObj.TryGetProperty("parts", out var partsArr) &&
                        partsArr.GetArrayLength() > 0 &&
                        partsArr[0].TryGetProperty("text", out var textProp))
                    {
                        responseText = textProp.GetString() ?? "";
                    }
                    else
                    {
                        responseText = "[ESCALATE]";
                    }

                    if (responseText.Contains("[ESCALATE]"))
                    {
                        if (!string.IsNullOrEmpty(dto.MaGiaoVien))
                        {
                            phanHoiCuaHeThong = "Trợ lý ảo: Ghi nhận ý kiến. Vấn đề này thuộc danh mục phức tạp/nhạy cảm cần Giáo viên giải đáp. Hệ thống đã chuyển cảnh báo cho Thầy/Cô.";
                        } 
                        else 
                        {
                            phanHoiCuaHeThong = "Trợ lý ảo: Ghi nhận ý kiến. Vấn đề này thuộc danh mục phức tạp/nhạy cảm cần Giáo viên Chủ nhiệm giải đáp. Hệ thống đã chuyển cảnh báo cho Thầy/Cô.";
                        }
                        phanHoi.TrangThai = "Chờ GV xử lý"; 
                    }
                    else if (responseText.Contains("[AGREE]"))
                    {
                        responseText = responseText.Replace("[AGREE]", "").Trim();
                        phanHoiCuaHeThong = responseText;
                        phanHoi.TrangThai = "Đã phản hồi"; 
                    }
                    else
                    {
                        phanHoiCuaHeThong = "Trợ lý ảo: " + responseText.Trim();
                        phanHoi.TrangThai = "AI đã trả lời";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LỖI GEMINI API]: {ex.Message}");
                    
                    // Cơ chế phòng vệ dự phòng lỗi API bằng logic C# Regex
                    string norm = noiDungGoc.ToLower();
                    bool containsTT27Keywords = norm.Contains("tt27") 
                        || norm.Contains("thông tư 27") 
                        || norm.Contains("thong tu 27")
                        || norm.Contains("hoàn thành") 
                        || norm.Contains("hoan thanh")
                        || norm.Contains("xếp loại") 
                        || norm.Contains("xep loai")
                        || norm.Contains("học lực") 
                        || norm.Contains("hoc luc")
                        || norm.Contains("đánh giá") 
                        || norm.Contains("danh gia")
                        || norm.Contains("lên lớp")
                        || norm.Contains("len lop")
                        || norm.Contains("môn")
                        || norm.Contains("đạo đức")
                        || norm.Contains("thể dục")
                        || norm.Contains("âm nhạc")
                        || norm.Contains("mỹ thuật")
                        || norm.Contains("hoạt động trải nghiệm")
                        || norm.Contains("hoat dong trai nghiem")
                        || norm.Contains("tự nhiên và xã hội")
                        || norm.Contains("tu nhien va xa hoi")
                        || norm.Contains("tin học")
                        || norm.Contains("tin hoc")
                        || norm.Contains("công nghệ")
                        || norm.Contains("cong nghe")
                        || norm.Contains("lịch sử")
                        || norm.Contains("lich su")
                        || norm.Contains("địa lý")
                        || norm.Contains("dia ly")
                        || System.Text.RegularExpressions.Regex.IsMatch(norm, @"\b(h|t|c)\b");

                    if (loaiThongBao == "KeHoach" && (norm.Contains("ok") || norm.Contains("đồng ý") || norm.Contains("nhất trí") || norm.Contains("đã nhận")))
                    {
                        phanHoiCuaHeThong = "Hệ thống trợ lý ảo: Đã ghi nhận phản hồi xác nhận của phụ huynh (dự phòng lỗi kết nối).";
                        phanHoi.TrangThai = "Đã phản hồi";
                    }
                    else if (containsTT27Keywords)
                    {
                        bool isT = System.Text.RegularExpressions.Regex.IsMatch(norm, @"\b(t|tốt|tot)\b") 
                            || norm.Contains("hoàn thành tốt") 
                            || norm.Contains("hoan thanh tot");

                        bool isC = System.Text.RegularExpressions.Regex.IsMatch(norm, @"\b(c|chưa|chua)\b") 
                            || norm.Contains("chưa hoàn thành") 
                            || norm.Contains("chua hoan thanh");

                        bool isH = System.Text.RegularExpressions.Regex.IsMatch(norm, @"\b(h)\b") 
                            || norm.Contains("hoàn thành") 
                            || norm.Contains("hoan thanh");

                        if (isH && !isT && !isC)
                        {
                            phanHoiCuaHeThong = "Trợ lý ảo (dự phòng): Dạ thưa phụ huynh, thầy cô rất trân trọng sự nỗ lực, ngoan ngoãn của bé trong học tập. Đánh giá H là bé hoàn thành mục tiêu bài học, còn muốn lên mức T thì bé phải có gì đó vượt trội hơn các bạn. Gia đình hãy tiếp tục khích lệ bé nhé! Chi tiết hướng dẫn tại [Tài Liệu Mở Rộng: /files/ThongTu27.pdf].";
                        }
                        else if (isT)
                        {
                            phanHoiCuaHeThong = "Trợ lý ảo (dự phòng): Dạ thưa phụ huynh, đánh giá T (Hoàn thành tốt) có nghĩa là bé hoàn thành xuất sắc các mục tiêu học tập và thể hiện rõ năng lực vượt trội hơn các bạn trong lớp. Thầy cô rất trân trọng nỗ lực rèn luyện của bé. Chi tiết hướng dẫn tại [Tài Liệu Mở Rộng: /files/ThongTu27.pdf].";
                        }
                        else if (isC)
                        {
                            phanHoiCuaHeThong = "Trợ lý ảo (dự phòng): Dạ thưa phụ huynh, đánh giá C (Chưa hoàn thành) cho thấy bé cần cố cố gắng hơn để đạt mục tiêu bài học. Thầy cô rất trân trọng sự nỗ lực rèn luyện của bé và sẽ kèm cặp để con sớm tiến bộ vượt trội. Chi tiết hướng dẫn tại [Tài Liệu Mở Rộng: /files/ThongTu27.pdf].";
                        }
                        else
                        {
                            phanHoiCuaHeThong = "Trợ lý ảo (dự phòng): Dạ thưa phụ huynh, theo Thông tư 27, các môn như Đạo đức, Thể dục, Âm nhạc... đánh giá dựa trên thái độ ngoan ngoãn, sự nỗ lực rèn luyện của bé để hoàn thành mục tiêu môn học (không chấm điểm số định lượng). Chi tiết hướng dẫn tại [Tài Liệu Mở Rộng: /files/ThongTu27.pdf].";
                        }
                        phanHoi.TrangThai = "AI đã trả lời";
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(dto.MaGiaoVien))
                        {
                            phanHoiCuaHeThong = "Trợ lý ảo: Ghi nhận ý kiến. Vấn đề này thuộc danh mục phức tạp/nhạy cảm cần Giáo viên giải đáp. Hệ thống đã chuyển cảnh báo cho Thầy/Cô.";
                        } 
                        else 
                        {
                            phanHoiCuaHeThong = "Trợ lý ảo: Ghi nhận ý kiến. Vấn đề này thuộc danh mục phức tạp/nhạy cảm cần Giáo viên Chủ nhiệm giải đáp. Hệ thống đã chuyển cảnh báo cho Thầy/Cô.";
                        }
                        phanHoi.TrangThai = "Chờ GV xử lý";
                    }
                }
            }
            // 3. Cập nhật trạng thái thông báo gốc (nếu là Plan)
            if (phanHoi.MaKeHoach.HasValue)
            {
                var thongBaoGoc = await _context.TuongTacs
                    .FirstOrDefaultAsync(t => t.MaKeHoach == phanHoi.MaKeHoach
                                          && t.TenDangNhap == phanHoi.TenDangNhap
                                          && t.TrangThai == "Chưa xem");
                if (thongBaoGoc != null)
                {
                    thongBaoGoc.TrangThai = "Đã phản hồi";
                }
            }

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
                    NoiDung = !string.IsNullOrEmpty(dto.MaGiaoVien) ? $"[FROM:{dto.MaGiaoVien.Trim()}] {phanHoiCuaHeThong}" : phanHoiCuaHeThong,
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

        public class PhanHoiDto
        {
            public int? MaKeHoach { get; set; }
            public string TenDangNhap { get; set; }
            public string NoiDung { get; set; }
            public string? TrangThai { get; set; }
            public string? MaGiaoVien { get; set; }
        }

        public class TinNhanDto
        {
            public string TenDangNhap { get; set; }
            public string NoiDung { get; set; }
            public string NguoiGui { get; set; } // "GiaoVien" hoặc "PhuHuynh"
        }

        /// <summary>
        /// API Gửi tin nhắn tự do (Không bám vào Kế hoạch lớp)
        /// Phục vụ cho tính năng Nhắn tin trực tiếp của GVBM và Giao tiếp Phụ huynh
        /// </summary>
        [HttpPost("gui-tin-nhan")]
        public async Task<IActionResult> GuiTinNhanTrucTiep([FromBody] TinNhanDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.NoiDung) || string.IsNullOrWhiteSpace(dto.TenDangNhap))
            {
                return BadRequest(new { message = "Dữ liệu tin nhắn không hợp lệ!" });
            }

            var maGiaoVien = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            string noiDungLuu = dto.NoiDung;

            if (dto.NguoiGui == "GiaoVien" && !string.IsNullOrEmpty(maGiaoVien))
            {
                // Nhúng Tag Tự Động Định Tuyến Người Gửi
                noiDungLuu = $"[FROM:{maGiaoVien}] " + (noiDungLuu.StartsWith("Giáo viên:") ? noiDungLuu : "Giáo viên: " + noiDungLuu);
            }

            var tinMoi = new TuongTac
            {
                TenDangNhap = dto.TenDangNhap,
                NoiDung = noiDungLuu,
                ThoiGian = DateTime.Now,
                TrangThai = dto.NguoiGui == "GiaoVien" ? "Chưa xem" : "Chờ GV xử lý"
            };

            _context.TuongTacs.Add(tinMoi);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Đã gửi thông tin kết nối thành công!" });
        }

        /// <summary>
        /// Cấp số liệu cho cái Chuông thông báo trên giao diện Front-end
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
            var soLuongKeHoach = await _context.TuongTacs
                .Include(t => t.MaKeHoachNavigation) // Kết nối sang bảng KeHoachLop
                .CountAsync(t => t.TrangThai == "Chờ GV xử lý" &&
                                 t.MaKeHoachNavigation != null &&
                                 t.MaKeHoachNavigation.NguoiDang == maGiaoVien); // Chốt chặn bảo mật ở đây!

            // 3. Đếm thêm tin nhắn "Chờ GV xử lý" dạng Tự_Do (MaKeHoach = null) dành cho GVCN
            // Lấy danh sách lớp chủ nhiệm
            var lopChuNhiems = await _context.LopHocs.Where(l => l.GvchuNhiem == maGiaoVien).Select(l => l.MaLop).ToListAsync();
            // Lấy danh sách tài khoản PH của các lớp này
            var danhSachPhuHuynh = await _context.HocSinhs.Where(h => lopChuNhiems.Contains(h.MaLop)).Select(h => h.TaiKhoanPhuHuynh).ToListAsync();
            
            var soLuongTuDo = await _context.TuongTacs
                .CountAsync(t => t.TrangThai == "Chờ GV xử lý" && 
                                 t.MaKeHoach == null && 
                                 danhSachPhuHuynh.Contains(t.TenDangNhap));

            // --- CHỈ BỔ SUNG ĐIỂM NÀY: ĐẾM RIÊNG KHÚC ĐUÔI CHO GVBM ---
            string maGvClean = maGiaoVien.Trim();
            var soLuongGVBM = await _context.TuongTacs
                .CountAsync(t => t.TrangThai.Contains("Chờ GV xử lý") && 
                                 t.MaKeHoach == null && 
                                 t.NoiDung.Contains("[TO:" + maGvClean + "]"));

            int soLuong = soLuongKeHoach + soLuongTuDo + soLuongGVBM;

            return Ok(new
            {
                soThongBaoChuaDoc = soLuong,
                message = soLuong > 0 ? $"Bạn có {soLuong} phản hồi cần xử lý" : "Không có thông báo mới"
            });

        }
        /// <summary>
        /// API dành cho Giáo viên trả lời thắc mắc của phụ huynh và tự động trừ chuông
        /// </summary>
        [HttpPost("giao-vien-tra-loi")]
        [Authorize] // Bắt buộc giáo viên phải đăng nhập
        public async Task<IActionResult> GiaoVienTraLoi([FromBody] GiaoVienTraLoiRequest model)
        {
            // 1. Kiểm tra dữ liệu đầu vào
            if (model == null || string.IsNullOrWhiteSpace(model.NoiDungTraLoi))
            {
                return BadRequest(new { message = "Vui lòng nhập nội dung câu trả lời!" });
            }

            // 2. Tìm tin nhắn gốc đang "Chờ GV xử lý" trong Database
            // 2. Lấy mã giáo viên đang đăng nhập từ Token (Chìa khóa bảo mật)
            var maGiaoVien = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // 3. Tìm tin nhắn gốc VÀ kết nối sang bảng KeHoachLop để kiểm tra chủ sở hữu
            var tinNhanGoc = await _context.TuongTacs
                .Include(t => t.MaKeHoachNavigation)
                .FirstOrDefaultAsync(t => t.MaTuongTac == model.MaTuongTacGoc);

            if (tinNhanGoc == null)
            {
                return NotFound(new { message = "Không tìm thấy câu hỏi gốc cần trả lời!" });
            }

            // CHỐT CHẶN BẢO MẬT: Kiểm tra xem giáo viên đang thao tác có đúng là GVCN hoặc giáo viên được chỉ định đích danh không!
            if (tinNhanGoc.MaKeHoachNavigation != null)
            {
                if (tinNhanGoc.MaKeHoachNavigation.NguoiDang != maGiaoVien)
                {
                    return StatusCode(403, new { message = "Lỗi bảo mật: Bạn không phải là GVCN phụ trách kế hoạch này nên không có quyền trả lời!" });
                }
            }
            else
            {
                string tagCheck = $"[TO:{maGiaoVien}]";
                bool laNguoiNhanDichDanh = (tinNhanGoc.NoiDung ?? "").Contains(tagCheck);

                var laGVCN = await _context.HocSinhs.AnyAsync(h => 
                    h.TaiKhoanPhuHuynh == tinNhanGoc.TenDangNhap && 
                    _context.LopHocs.Any(l => l.MaLop == h.MaLop && l.GvchuNhiem == maGiaoVien));

                if (!laGVCN && !laNguoiNhanDichDanh)
                {
                    return StatusCode(403, new { message = "Lỗi bảo mật: Bạn không quản lý lớp khách hàng của phụ huynh này và tin nhắn không gửi đích danh cho bạn!" });
                }
            }

            // 3. CẬP NHẬT TRẠNG THÁI (Chốt chặn giúp trừ chuông ở đây!)
            // Đổi từ "Chờ GV xử lý" -> "Đã phản hồi" để hàm DemSoTinNhanCho không đếm nó nữa
            tinNhanGoc.TrangThai = "Đã phản hồi";

            // 4. TẠO TIN NHẮN TRẢ LỜI CỦA GIÁO VIÊN
            var checkGVCN = await _context.HocSinhs.AnyAsync(h => 
                h.TaiKhoanPhuHuynh == tinNhanGoc.TenDangNhap && 
                _context.LopHocs.Any(l => l.MaLop == h.MaLop && l.GvchuNhiem == maGiaoVien));

            string prefixText = checkGVCN ? "Giáo viên chủ nhiệm" : "Giáo viên bộ môn";

            var phanHoiCuaGV = new TuongTac
            {
                MaKeHoach = tinNhanGoc.MaKeHoach,
                TenDangNhap = tinNhanGoc.TenDangNhap, // Giữ nguyên mã của phụ huynh để tin nhắn bay về đúng hộp thư của họ
                NoiDung = $"[FROM:{maGiaoVien}] {prefixText}: " + model.NoiDungTraLoi.Trim(),
                ThoiGian = DateTime.Now,
                TrangThai = "Giáo viên trả lời"
            };

            _context.TuongTacs.Add(phanHoiCuaGV);

            // 5. Lưu tất cả thay đổi vào Database
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Gửi phản hồi thành công! Chuông thông báo đã được trừ đi 1.",
                data = phanHoiCuaGV
            });
        }
        [HttpGet("hop-thu-giao-vien/{maGiaoVien}")]
        public async Task<IActionResult> LayHopThuGiaoVien(string maGiaoVien)
        {
            if (string.IsNullOrEmpty(maGiaoVien)) return BadRequest("Mã giáo viên không hợp lệ!");
            maGiaoVien = maGiaoVien.Trim();

            // 1. Lớp chủ nhiệm
            var lopChuNhiems = await _context.LopHocs
                .Where(l => l.GvchuNhiem.Trim() == maGiaoVien)
                .Select(l => l.MaLop.Trim())
                .ToListAsync();

            // 2. Lớp dạy bộ môn
            var lopBoMons = await _context.PhanCongGiangDays
                .Where(p => p.MaGiaoVien.Trim() == maGiaoVien)
                .Select(p => p.MaLop.Trim())
                .ToListAsync();

            var danhSachLop = lopChuNhiems.Union(lopBoMons).Distinct().ToList();

            // 3. Lấy tập hợp Phụ huynh thuộc các lớp trên
            var danhSachPhuHuynh = await _context.HocSinhs
                .Where(hs => danhSachLop.Contains(hs.MaLop.Trim()) && !string.IsNullOrEmpty(hs.TaiKhoanPhuHuynh))
                .Select(hs => hs.TaiKhoanPhuHuynh.Trim())
                .Distinct()
                .ToListAsync();

            // 4. Quét toàn bộ hộp thư gom vào 1 dòng suối duy nhất (Có đính kèm Kế hoạch để lọc)
            var danhSachTinNhan = await _context.TuongTacs
                .Include(t => t.MaKeHoachNavigation)
                .Where(t => danhSachPhuHuynh.Contains(t.TenDangNhap.Trim()) || t.TenDangNhap.Trim() == maGiaoVien)
                .OrderByDescending(t => t.ThoiGian)
                .ToListAsync();

            // 5. Màng lọc ABAC Phân mảnh Quyền riêng tư (Privacy Leak Prevention)
            var ketQuaGiaoVien = new List<TuongTac>();
            
            // Xây dựng bộ từ điển tra cứu nhanh Lớp Của Phụ Huynh (đề phòng phụ huynh có nhiều con)
            var hocSinhLookup = await _context.HocSinhs
                .Where(hs => danhSachPhuHuynh.Contains(hs.TaiKhoanPhuHuynh.Trim()))
                .Select(hs => new { TaiKhoanPhuHuynh = hs.TaiKhoanPhuHuynh.Trim(), MaLop = hs.MaLop.Trim() })
                .ToListAsync();

            foreach (var t in danhSachTinNhan)
            {
                // TH1: Tin nhắn mọc ra từ một Kế Hoạch (Plan-based message)
                if (t.MaKeHoach != null)
                {
                    // Chỉ người đăng kế hoạch đó mới được seen!
                    if (t.MaKeHoachNavigation != null && t.MaKeHoachNavigation.NguoiDang.Trim() == maGiaoVien)
                    {
                        ketQuaGiaoVien.Add(t);
                    }
                }
                // TH2: Tin nhắn tự do (Direct Message, MaKeHoach = null)
                else if (!string.IsNullOrEmpty(t.TenDangNhap))
                {
                    string currentTenDangNhap = t.TenDangNhap.Trim();
                    // Lấy ra TẤT CẢ các lớp học mà phụ huynh này đang gửi con theo học
                    var cacLopCuaPhuHuynh = hocSinhLookup
                        .Where(x => x.TaiKhoanPhuHuynh == currentTenDangNhap)
                        .Select(x => x.MaLop)
                        .ToList();

                    // Nếu ông giáo viên ĐANG MỞ HỘP THƯ làm GVCN của ÍT NHẤT MỘT lớp trong danh sách trên 
                    bool laGVCN = cacLopCuaPhuHuynh.Any(l => lopChuNhiems.Contains(l));

                    string noiDungCheck = (t.NoiDung ?? "").Trim();
                    bool laTinCuaMinh = noiDungCheck.StartsWith($"[FROM:{maGiaoVien}]");
                    bool laTinDanhGiengMinh = noiDungCheck.Contains($"[TO:{maGiaoVien}]");
                    bool laTinNguoiKhacXacDinh = noiDungCheck.Contains("[TO:") && !laTinDanhGiengMinh;
                    bool laTinTuGVKhac = noiDungCheck.StartsWith("[FROM:") && !laTinCuaMinh;

                    if (laTinCuaMinh || laTinDanhGiengMinh)
                    {
                        // Quyền tuyệt đối: Tin tự nhắn ra hoặc tin PH nhắn riêng cho mình -> Vô hộp!
                        ketQuaGiaoVien.Add(t);
                    }
                    else if (laGVCN && !laTinTuGVKhac && !laTinNguoiKhacXacDinh)
                    {
                        // GVCN thầu các tin nhắn chung của phụ huynh không đính tên ai cụ thể
                        ketQuaGiaoVien.Add(t);
                    }
                }
            }

            // Gỡ bỏ liên kết vòng lặp Json để tránh lỗi lặp vô tận khi gửi về Node.js
            foreach (var kq in ketQuaGiaoVien)
            {
                kq.MaKeHoachNavigation = null;
            }

            return Ok(ketQuaGiaoVien);
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
        }
    }
    public class GiaoVienTraLoiRequest
    {
        public int MaTuongTacGoc { get; set; } // ID của tin nhắn đang làm chuông reo
        public string NoiDungTraLoi { get; set; } // Câu trả lời của giáo viên
    }

}