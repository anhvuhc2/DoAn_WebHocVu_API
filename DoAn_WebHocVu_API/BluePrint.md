HỆ THỐNG QUẢN LÝ HỌC VỤ VÀ TƯƠNG TÁC PHỤ HUYNH
(Academic Management and Parent Interaction System - Web Học Vụ API)

1. Kiến Trúc Hệ Thống (N-Tier / API Architecture)
Hệ thống được thiết kế theo mô hình kiến trúc phân tầng, tách biệt rõ ràng giữa cơ sở dữ liệu, logic xử lý nghiệp vụ và giao diện tương tác. Việc này giúp hệ thống dễ dàng bảo trì, mở rộng và đảm bảo tính bảo mật khi giao tiếp qua các Endpoints (RESTful API).

Hệ thống được cấu trúc thành các tầng cốt lõi:
┌─────────────────────────────────────────┐
│          Client (Web/App UI)            │
│   ┌─────────────────────────────────┐   │
│   │   API Controllers (Endpoints)   │   │
│   │   ┌─────────────────────────┐   │   │
│   │   │ Business Logic / AI Core│   │   │
│   │   │   ┌─────────────────┐   │   │   │
│   │   │   │ Models / Entity │   │   │   │
│   │   │   │  (DB Context)   │   │   │   │
│   │   │   └─────────────────┘   │   │   │
│   │   └─────────────────────────┘   │   │
│   └─────────────────────────────────┘   │
└─────────────────────────────────────────┘

1.1. Tầng Thực Thể & Dữ Liệu (Models Layer)
Là trung tâm lưu trữ các thực thể ánh xạ trực tiếp với Cơ sở dữ liệu thông qua Entity Framework.

Thành phần chính: Các lớp HocSinh (Học sinh), LopHoc (Lớp học), KeHoachLop (Kế hoạch), TuongTac (Tương tác/Phản hồi).

1.2. Tầng Logic Nghiệp Vụ (Business Logic Layer)
Nơi xử lý các quy tắc nghiệp vụ đặc thù của trường học, ví dụ như quy tắc xét duyệt điểm, phân loại trạng thái học sinh, và đặc biệt là bộ lọc AI phân tích ngữ cảnh tin nhắn.

Thành phần chính: Logic đánh giá theo Thông tư 27, Logic xử lý phân luồng tin nhắn (Báo điểm vs Kế hoạch).

1.3. Tầng Bộ Điều Khiển (Controllers / API Layer)
Đóng vai trò là cửa ngõ tiếp nhận các Request từ giao diện (Front-end), gọi các logic xử lý bên dưới và trả về dữ liệu chuẩn JSON.

Thành phần chính: HocSinhController, LopHocController, TuongTacController, KeHoachController.

1.4. Tầng Ngoại Vi & Lưu Trữ (Infrastructure Layer)
Cung cấp nền tảng lưu trữ vật lý cho dữ liệu và các tệp đính kèm.

Thành phần chính: Hệ quản trị CSDL SQL Server, Hệ thống lưu trữ tệp (File Storage cho tệp đính kèm kế hoạch).

2. Công Nghệ Sử Dụng (Tech Stack)
Để vận hành một hệ thống quản lý học vụ ổn định, xử lý mượt mà khối lượng dữ liệu điểm số và thông báo hàng ngày, các công nghệ sau được áp dụng:

2.1. Tầng Backend (Máy chủ & Xử lý logic)
Thành phần	Công nghệ đề xuất	Lý do lựa chọn
Framework chính	C# .NET Core / ASP.NET API	Nền tảng mạnh mẽ, bảo mật cao, hỗ trợ Dependency Injection và xây dựng RESTful API cực kỳ chuẩn mực, phù hợp cho môi trường giáo dục.
Cơ sở dữ liệu	Microsoft SQL Server 2014	Đảm bảo tính toàn vẹn dữ liệu cho các hệ thống bảng liên kết chặt chẽ (Khóa ngoại, Transactions, Stored Procedures).
Giao tiếp Dữ liệu	Entity Framework Core	Truy vấn dữ liệu qua ORM, tối ưu hóa quá trình thao tác với SQL mà không cần viết quá nhiều script thuần.
Bảo mật & Phân quyền	JWT (JSON Web Token)	Mã hóa phiên đăng nhập, phân tách rõ ràng quyền hạn giữa Ban giám hiệu, Giáo viên và Phụ huynh.
2.2. Tầng Giao Diện (Frontend / Client-side)
Nền tảng giao diện: Có thể tích hợp linh hoạt với các ứng dụng Web (React.js / Vue.js) hoặc Mobile App thông qua các API Endpoints đã được chuẩn hóa trên Swagger.

Mục đích: Xây dựng cổng thông tin minh bạch, thân thiện giúp Giáo viên thao tác nhanh và Phụ huynh nhận thông báo tức thời.

3. Các Phân Hệ Chức Năng (Hệ Thống 4 Tác Vụ Cốt Lõi)
Hệ thống được thiết kế bám sát quy trình tác nghiệp thực tế tại trường học, bao gồm 4 nhiệm vụ trọng tâm:

Module 1: Quản trị Hồ sơ & Lớp học (Class & Profile Management)
Phân hệ nền tảng giúp thiết lập dữ liệu ban đầu cho mỗi năm học, hỗ trợ quy trình nhập liệu linh hoạt.

Quản lý Lớp học: Phân công Giáo viên chủ nhiệm, quản lý sĩ số, cập nhật trạng thái lớp.

Truy xuất Hồ sơ Học sinh: Phân tách rõ ràng giữa việc lấy "Danh sách sĩ số hiện tại" (phục vụ điểm danh, tác vụ hàng ngày của GV) và "Truy xuất toàn bộ hồ sơ" (phục vụ thống kê, báo cáo của BGH, bao gồm cả học sinh đã chuyển trường).

Quản lý Tài khoản linh hoạt: Cho phép khởi tạo hồ sơ học sinh trước (để ổn định sĩ số) và cập nhật/liên kết tài khoản phụ huynh vào hệ thống sau mà không gây gián đoạn quy trình.

Module 2: Quản lý Đánh giá & Báo điểm (Grading & Evaluation)
Số hóa quy trình đánh giá kết quả học tập theo chuẩn quy định hiện hành.

Quản lý Bảng điểm: Lưu trữ điểm số chi tiết từng môn học.

Tích hợp Thông tư 27: Chuẩn hóa hệ thống đánh giá (H - Hoàn thành, T - Hoàn thành Tốt, C - Chưa hoàn thành) đối với các môn đánh giá bằng nhận xét, giúp giáo viên phân loại và xuất bảng điểm nhanh chóng.

Tự động hóa thông báo: Hỗ trợ trích xuất và gửi trực tiếp kết quả học tập cá nhân hóa đến từng tài khoản phụ huynh tương ứng.

Module 3: Quản lý Kế hoạch & Sự kiện Lớp (Class Planning & Notifications)
Phân hệ giúp giáo viên truyền đạt thông tin không liên quan đến điểm số (thu phí, dã ngoại, sự kiện phong trào).

Đăng tải thông báo chung: Giáo viên khởi tạo nội dung và gửi đồng loạt đến toàn bộ phụ huynh trong lớp chỉ với một thao tác.

Quản lý Tệp đính kèm (File Upload): Cho phép đính kèm các văn bản, hình ảnh, file PDF (kế hoạch chi tiết, thông báo đóng dấu đỏ) trực tiếp vào nội dung thông báo.

Theo dõi trạng thái đọc: Quản lý hộp thư độc lập cho từng phụ huynh để hệ thống biết được ai đã nhận và đọc thông báo.

Module 4: Tương Tác Cư Dân & Trợ Lý Ảo AI (Smart Interaction & Virtual Assistant)
(Đây là tác vụ nâng cao - điểm nhấn của hệ thống)
Module tự động hóa việc tiếp nhận phản hồi từ phụ huynh, giảm tải áp lực trực tin nhắn cho giáo viên.

Phân tích bối cảnh (Context Awareness): Hệ thống có khả năng nhận diện tin nhắn của phụ huynh đang phản hồi cho "Loại thông báo" nào (Báo điểm hay Kế hoạch sự kiện).

Trợ lý ảo xử lý Báo điểm: * Tự động giải thích các thắc mắc về hệ thống đánh giá chữ (H, T, C) theo chuẩn quy định.

Nhận diện các từ khóa thắc mắc về môn phụ (Đạo đức, Thể dục...) và tự động phản hồi giải thích quy chế quan sát không dùng điểm số.

Trợ lý ảo xử lý Kế hoạch lớp:

Nhận diện và tự động đánh dấu "Đã xử lý" đối với các tin nhắn xác nhận đơn giản (vd: "Tôi đồng ý", "Nhất trí", "Ok").

Tự động phân luồng (rung chuông cảnh báo) chuyển đến Giáo viên chủ nhiệm đối với các phản hồi chứa câu hỏi thắc mắc, ý kiến trái chiều hoặc cần giải quyết chi tiết.