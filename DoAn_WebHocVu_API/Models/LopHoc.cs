using System;
using System.Collections.Generic;

namespace DoAn_WebHocVu_API.Models;

public partial class LopHoc
{
    public string MaLop { get; set; } = null!;

    public string TenLop { get; set; } = null!;

    public string? NienKhoa { get; set; }

    public string? GvchuNhiem { get; set; }

    public virtual TaiKhoan? GvchuNhiemNavigation { get; set; }

    public virtual ICollection<HocSinh> HocSinhs { get; set; } = new List<HocSinh>();

    public virtual ICollection<KeHoachLop> KeHoachLops { get; set; } = new List<KeHoachLop>();

    public virtual ICollection<PhanCongGiangDay> PhanCongGiangDays { get; set; } = new List<PhanCongGiangDay>();
}
