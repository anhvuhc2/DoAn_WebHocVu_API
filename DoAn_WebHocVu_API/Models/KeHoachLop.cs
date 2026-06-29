using System;
using System.Collections.Generic;

namespace DoAn_WebHocVu_API.Models;

public partial class KeHoachLop
{
    public int MaKeHoach { get; set; }

    public string? MaLop { get; set; }

    public string TieuDe { get; set; } = null!;

    public string NoiDung { get; set; } = null!;

    public string? LoaiThongBao { get; set; }

    public DateTime? NgayDang { get; set; }

    public string? NguoiDang { get; set; }

    public virtual LopHoc? MaLopNavigation { get; set; }

    public virtual TaiKhoan? NguoiDangNavigation { get; set; }

    public virtual ICollection<TuongTac> TuongTacs { get; set; } = new List<TuongTac>();
    public string? FileDinhKem { get; set; }
}
