using System;
using System.Collections.Generic;

namespace DoAn_WebHocVu_API.Models;
public partial class BangDiem
{
    public int Id { get; set; }

    public string? MaHs { get; set; }

    public string? MaMon { get; set; }

    public double? DiemThi { get; set; }

    public string? XepLoai { get; set; }

    public string? NhanXet { get; set; }

    public DateTime? NgayCapNhat { get; set; }

    public virtual HocSinh? MaHsNavigation { get; set; }

    public virtual MonHoc? MaMonNavigation { get; set; }
}
