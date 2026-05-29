using System;
using System.Collections.Generic;

namespace DoAn_WebHocVu_API.Models;

public partial class MonHoc
{
    public string MaMon { get; set; } = null!;

    public string TenMon { get; set; } = null!;

    public int? SoTinChi { get; set; }
    public string? LoaiMon { get; set; }

    public virtual ICollection<BangDiem> BangDiems { get; set; } = new List<BangDiem>();

    public virtual ICollection<PhanCongGiangDay> PhanCongGiangDays { get; set; } = new List<PhanCongGiangDay>();
}
