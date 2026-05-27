using System;
using System.Collections.Generic;

namespace DoAn_WebHocVu_API.Models;

public partial class HocSinh
{
    public string MaHs { get; set; } = null!;

    public string HoTen { get; set; } = null!;

    public DateOnly? NgaySinh { get; set; }

    public string? MaLop { get; set; }

    public string? TaiKhoanPhuHuynh { get; set; }

    public virtual ICollection<BangDiem> BangDiems { get; set; } = new List<BangDiem>();

    public virtual ICollection<DiemDanh> DiemDanhs { get; set; } = new List<DiemDanh>();

    public virtual LopHoc? MaLopNavigation { get; set; }

    public virtual TaiKhoan? TaiKhoanPhuHuynhNavigation { get; set; }
}
