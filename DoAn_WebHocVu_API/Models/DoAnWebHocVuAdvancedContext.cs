using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace DoAn_WebHocVu_API.Models;

public partial class DoAnWebHocVuAdvancedContext : DbContext
{
    public DoAnWebHocVuAdvancedContext()
    {
    }

    public DoAnWebHocVuAdvancedContext(DbContextOptions<DoAnWebHocVuAdvancedContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BangDiem> BangDiems { get; set; }

    public virtual DbSet<DiemDanh> DiemDanhs { get; set; }

    public virtual DbSet<HocSinh> HocSinhs { get; set; }

    public virtual DbSet<KeHoachLop> KeHoachLops { get; set; }

    public virtual DbSet<LopHoc> LopHocs { get; set; }

    public virtual DbSet<MonHoc> MonHocs { get; set; }

    public virtual DbSet<PhanCongGiangDay> PhanCongGiangDays { get; set; }

    public virtual DbSet<TaiKhoan> TaiKhoans { get; set; }

    public virtual DbSet<TuongTac> TuongTacs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=ADMIN;Database=DoAn_WebHocVu_Advanced;User Id=sa;Password=123456;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BangDiem>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__BangDiem__3214EC278AFDC456");

            entity.ToTable("BangDiem");

            entity.HasIndex(e => new { e.MaHs, e.MaMon }, "UQ__BangDiem__B4801474B112227A").IsUnique();

            entity.Property(e => e.Id).HasColumnName("ID");
            
            entity.Property(e => e.MaHs)
                .HasMaxLength(20)
                .HasColumnName("MaHS");
            entity.Property(e => e.MaMon).HasMaxLength(20);
            entity.Property(e => e.NgayCapNhat)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.MaHsNavigation).WithMany(p => p.BangDiems)
                .HasForeignKey(d => d.MaHs)
                .HasConstraintName("FK__BangDiem__MaHS__239E4DCF");

            entity.HasOne(d => d.MaMonNavigation).WithMany(p => p.BangDiems)
                .HasForeignKey(d => d.MaMon)
                .HasConstraintName("FK__BangDiem__MaMon__24927208");
        });

        modelBuilder.Entity<DiemDanh>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__DiemDanh__3214EC277D679F02");

            entity.ToTable("DiemDanh");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.MaHs)
                .HasMaxLength(20)
                .HasColumnName("MaHS");
            entity.Property(e => e.NgayVang).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.NguoiDiemDanh).HasMaxLength(50);
            entity.Property(e => e.TrangThai).HasMaxLength(50);

            entity.HasOne(d => d.MaHsNavigation).WithMany(p => p.DiemDanhs)
                .HasForeignKey(d => d.MaHs)
                .HasConstraintName("FK__DiemDanh__MaHS__2A4B4B5E");

            entity.HasOne(d => d.NguoiDiemDanhNavigation).WithMany(p => p.DiemDanhs)
                .HasForeignKey(d => d.NguoiDiemDanh)
                .HasConstraintName("FK__DiemDanh__NguoiD__2D27B809");
        });

        modelBuilder.Entity<HocSinh>(entity =>
        {
            entity.HasKey(e => e.MaHs).HasName("PK__HocSinh__2725A6EF6BB57EAE");

            entity.ToTable("HocSinh");

            entity.Property(e => e.MaHs)
                .HasMaxLength(20)
                .HasColumnName("MaHS");
            entity.Property(e => e.HoTen).HasMaxLength(100);
            entity.Property(e => e.MaLop).HasMaxLength(20);
            entity.Property(e => e.TaiKhoanPhuHuynh).HasMaxLength(50);

            entity.HasOne(d => d.MaLopNavigation).WithMany(p => p.HocSinhs)
                .HasForeignKey(d => d.MaLop)
                .HasConstraintName("FK__HocSinh__MaLop__1ED998B2");

            entity.HasOne(d => d.TaiKhoanPhuHuynhNavigation).WithMany(p => p.HocSinhs)
                .HasForeignKey(d => d.TaiKhoanPhuHuynh)
                .HasConstraintName("FK__HocSinh__TaiKhoa__1FCDBCEB");
        });

        modelBuilder.Entity<KeHoachLop>(entity =>
        {
            entity.HasKey(e => e.MaKeHoach).HasName("PK__KeHoachL__88C5741F13AC35E0");

            entity.ToTable("KeHoachLop");

            entity.Property(e => e.LoaiThongBao).HasMaxLength(50);
            entity.Property(e => e.MaLop).HasMaxLength(20);
            entity.Property(e => e.NgayDang)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.NguoiDang).HasMaxLength(50);
            entity.Property(e => e.TieuDe).HasMaxLength(200);

            entity.HasOne(d => d.MaLopNavigation).WithMany(p => p.KeHoachLops)
                .HasForeignKey(d => d.MaLop)
                .HasConstraintName("FK__KeHoachLo__MaLop__300424B4");

            entity.HasOne(d => d.NguoiDangNavigation).WithMany(p => p.KeHoachLops)
                .HasForeignKey(d => d.NguoiDang)
                .HasConstraintName("FK__KeHoachLo__Nguoi__31EC6D26");
        });

        modelBuilder.Entity<LopHoc>(entity =>
        {
            entity.HasKey(e => e.MaLop).HasName("PK__LopHoc__3B98D27394AC3EAC");

            entity.ToTable("LopHoc");

            entity.Property(e => e.MaLop).HasMaxLength(20);
            entity.Property(e => e.GvchuNhiem)
                .HasMaxLength(50)
                .HasColumnName("GVChuNhiem");
            entity.Property(e => e.NienKhoa).HasMaxLength(20);
            entity.Property(e => e.TenLop).HasMaxLength(50);

            entity.HasOne(d => d.GvchuNhiemNavigation).WithMany(p => p.LopHocs)
                .HasForeignKey(d => d.GvchuNhiem)
                .HasConstraintName("FK__LopHoc__GVChuNhi__1367E606");
        });

        modelBuilder.Entity<MonHoc>(entity =>
        {
            entity.HasKey(e => e.MaMon).HasName("PK__MonHoc__3A5B29A8CF77CBCF");

            entity.ToTable("MonHoc");

            entity.Property(e => e.MaMon).HasMaxLength(20);
            entity.Property(e => e.SoTinChi).HasDefaultValue(0);
            entity.Property(e => e.TenMon).HasMaxLength(100);
        });

        modelBuilder.Entity<PhanCongGiangDay>(entity =>
        {
            entity.HasKey(e => e.MaPhanCong).HasName("PK__PhanCong__C279D9163D9176C5");

            entity.ToTable("PhanCongGiangDay");

            entity.HasIndex(e => new { e.MaGiaoVien, e.MaLop, e.MaMon }, "UQ__PhanCong__07B4995F7E464DEF").IsUnique();

            entity.Property(e => e.MaGiaoVien).HasMaxLength(50);
            entity.Property(e => e.MaLop).HasMaxLength(20);
            entity.Property(e => e.MaMon).HasMaxLength(20);

            entity.HasOne(d => d.MaGiaoVienNavigation).WithMany(p => p.PhanCongGiangDays)
                .HasForeignKey(d => d.MaGiaoVien)
                .HasConstraintName("FK__PhanCongG__MaGia__1A14E395");

            entity.HasOne(d => d.MaLopNavigation).WithMany(p => p.PhanCongGiangDays)
                .HasForeignKey(d => d.MaLop)
                .HasConstraintName("FK__PhanCongG__MaLop__1B0907CE");

            entity.HasOne(d => d.MaMonNavigation).WithMany(p => p.PhanCongGiangDays)
                .HasForeignKey(d => d.MaMon)
                .HasConstraintName("FK__PhanCongG__MaMon__1BFD2C07");
        });

        modelBuilder.Entity<TaiKhoan>(entity =>
        {
            entity.HasKey(e => e.TenDangNhap).HasName("PK__TaiKhoan__55F68FC169EC2F0B");

            entity.ToTable("TaiKhoan");

            entity.Property(e => e.TenDangNhap).HasMaxLength(50);
            entity.Property(e => e.HoTen).HasMaxLength(100);
            entity.Property(e => e.MatKhau).HasMaxLength(255);
            entity.Property(e => e.VaiTro).HasMaxLength(20);
        });

        modelBuilder.Entity<TuongTac>(entity =>
        {
            entity.HasKey(e => e.MaTuongTac).HasName("PK__TuongTac__E947A5AC64D6578E");

            entity.ToTable("TuongTac");

            entity.Property(e => e.TenDangNhap).HasMaxLength(50);
            entity.Property(e => e.ThoiGian)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.MaKeHoachNavigation).WithMany(p => p.TuongTacs)
                .HasForeignKey(d => d.MaKeHoach)
                .HasConstraintName("FK__TuongTac__MaKeHo__34C8D9D1");

            entity.HasOne(d => d.TenDangNhapNavigation).WithMany(p => p.TuongTacs)
                .HasForeignKey(d => d.TenDangNhap)
                .HasConstraintName("FK__TuongTac__TenDan__35BCFE0A");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
