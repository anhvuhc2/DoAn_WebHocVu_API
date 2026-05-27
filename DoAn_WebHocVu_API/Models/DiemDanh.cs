using System;
using System.Collections.Generic;

namespace DoAn_WebHocVu_API.Models;

public partial class DiemDanh
{
    public int Id { get; set; }

    public string? MaHs { get; set; }

    public DateOnly? NgayVang { get; set; }

    public string? TrangThai { get; set; }

    public string? NguoiDiemDanh { get; set; }

    public virtual HocSinh? MaHsNavigation { get; set; }

    public virtual TaiKhoan? NguoiDiemDanhNavigation { get; set; }
}
