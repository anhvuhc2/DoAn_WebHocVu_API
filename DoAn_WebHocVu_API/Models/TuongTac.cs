using System;
using System.Collections.Generic;

namespace DoAn_WebHocVu_API.Models;

public partial class TuongTac
{
    public int MaTuongTac { get; set; }

    public int? MaKeHoach { get; set; }

    public string? TenDangNhap { get; set; }

    public string NoiDung { get; set; } = null!;
    public string? TrangThai { get; set; }

    public DateTime? ThoiGian { get; set; }

    public virtual KeHoachLop? MaKeHoachNavigation { get; set; }

    public virtual TaiKhoan? TenDangNhapNavigation { get; set; }
}
