using System;
using System.Collections.Generic;

namespace DoAn_WebHocVu_API.Models;

public partial class TaiKhoan
{
    public string TenDangNhap { get; set; } = null!;

    public string MatKhau { get; set; } = null!;

    public string HoTen { get; set; } = null!;

    public string? VaiTro { get; set; }

    public virtual ICollection<DiemDanh> DiemDanhs { get; set; } = new List<DiemDanh>();

    public virtual ICollection<HocSinh> HocSinhs { get; set; } = new List<HocSinh>();

    public virtual ICollection<KeHoachLop> KeHoachLops { get; set; } = new List<KeHoachLop>();

    public virtual ICollection<LopHoc> LopHocs { get; set; } = new List<LopHoc>();

    public virtual ICollection<PhanCongGiangDay> PhanCongGiangDays { get; set; } = new List<PhanCongGiangDay>();

    public virtual ICollection<TuongTac> TuongTacs { get; set; } = new List<TuongTac>();
}
