using System;
using System.Collections.Generic;
using HomeFinder.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HomeFinder.Context;

public partial class HomeFinderContext : DbContext
{
    public HomeFinderContext()
    {
    }

    public HomeFinderContext(DbContextOptions<HomeFinderContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Address> Addresses { get; set; }

    public virtual DbSet<Administrator> Administrators { get; set; }

    public virtual DbSet<Apartment> Apartments { get; set; }

    public virtual DbSet<Appointment> Appointments { get; set; }

    public virtual DbSet<Favorite> Favorites { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Photo> Photos { get; set; }

    public virtual DbSet<ReviewApartment> ReviewApartments { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserLoginLog> UserLoginLogs { get; set; }

    public virtual DbSet<LandlordSubscription> LandlordSubscriptions { get; set; }

    public virtual DbSet<ApartmentViewLog> ApartmentViewLogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        optionsBuilder.UseSqlServer("Server=DESKTOP-PQDG5QV;Database=HomeFinder;Trusted_Connection=True;TrustServerCertificate=True");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LandlordSubscription>(entity =>
        {
            entity.ToTable("LandlordSubscriptions", "dbo");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.StripeCustomerId).HasColumnName("stripe_customer_id").HasMaxLength(255).IsUnicode();
            entity.Property(e => e.StripeSubscriptionId).HasColumnName("stripe_subscription_id").HasMaxLength(255).IsUnicode();
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(50).IsUnicode();
            entity.Property(e => e.CurrentPeriodEndUtc).HasColumnName("current_period_end_utc");
            entity.Property(e => e.UpdatedAtUtc).HasColumnName("updated_at_utc");

            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.StripeSubscriptionId).IsUnique();
        });

        modelBuilder.Entity<Address>(entity =>
        {
            entity.HasKey(e => e.AddressId).HasName("PK__Address__CAA247C819578DA4");

            entity.ToTable("Address");

            entity.HasIndex(e => e.AddressId, "UQ__Address__CAA247C9A9CDB487").IsUnique();

            entity.Property(e => e.AddressId).HasColumnName("address_id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.ApartmentNumber)
                .HasMaxLength(20)
                .IsUnicode()
                .HasColumnName("apartment_number");
            entity.Property(e => e.BuildingNumber)
                .HasMaxLength(20)
                .IsUnicode()
                .HasColumnName("building_number");
            entity.Property(e => e.City)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("city");
            entity.Property(e => e.District)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("district");
            entity.Property(e => e.Latitude)
                .HasColumnType("decimal(18, 0)")
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnType("decimal(18, 0)")
                .HasColumnName("longitude");
            entity.Property(e => e.Region)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("region");
            entity.Property(e => e.StreetAddress)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("street_address");

            entity.HasOne(d => d.Apartment).WithMany(p => p.Addresses)
                .HasForeignKey(d => d.ApartmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Address__apartme__6C190EBB");
        });

        modelBuilder.Entity<Administrator>(entity =>
        {
            entity.HasKey(e => e.AdministratorId).HasName("PK__Administ__3871E7AC10D86DA4");

            entity.ToTable("Administrator");

            entity.HasIndex(e => e.AdministratorId, "UQ__Administ__3871E7AD412B365C").IsUnique();

            entity.Property(e => e.AdministratorId).HasColumnName("administrator_id");
            entity.Property(e => e.Login)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("login");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("password");
        });

        modelBuilder.Entity<Apartment>(entity =>
        {
            entity.HasKey(e => e.ApartmentId).HasName("PK__Apartmen__DC51C2EC9A8FCAC2");

            entity.ToTable("Apartment");

            entity.HasIndex(e => e.ApartmentId, "UQ__Apartmen__DC51C2ED744BE6EB").IsUnique();

            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Price)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("price");
            entity.Property(e => e.Rooms)
                .HasDefaultValue(1)
                .HasColumnName("rooms");
            entity.Property(e => e.Size)
                .HasDefaultValue(1)
                .HasColumnName("size");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Apartments)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Apartment__user___656C112C");
        });

        modelBuilder.Entity<ApartmentViewLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.ToTable("ApartmentViewLog", "dbo");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.ViewedAt)
                .HasColumnType("datetime")
                .HasColumnName("viewed_at");
            entity.HasOne(d => d.Apartment).WithMany()
                .HasForeignKey(d => d.ApartmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.AppointmentId).HasName("PK__Appointm__A50828FC348EAF35");

            entity.ToTable("Appointment");

            entity.HasIndex(e => e.AppointmentId, "UQ__Appointm__A50828FDA0265C85").IsUnique();

            entity.Property(e => e.AppointmentId).HasColumnName("appointment_id");
            entity.Property(e => e.AddressId).HasColumnName("address_id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.DateTime)
                .HasColumnType("datetime")
                .HasColumnName("date_time");

            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Address).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.AddressId)
                .HasConstraintName("FK__Appointme__addre__70DDC3D8");

            entity.HasOne(d => d.Apartment).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("FK__Appointme__apart__6FE99F9F");

            entity.HasOne(d => d.User).WithMany()
                .HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(e => e.FavoriteId).HasName("PK__Favorite__46ACF4CB41B10C30");

            entity.ToTable("Favorite");

            entity.HasIndex(e => e.FavoriteId, "UQ__Favorite__46ACF4CAD1661F53").IsUnique();

            entity.Property(e => e.FavoriteId).HasColumnName("favorite_id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Apartment).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.ApartmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Favorite__apartm__797309D9");

            entity.HasOne(d => d.User).WithMany(p => p.Favorites)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Favorite__user_i__787EE5A0");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.ClickTransId).HasName("PK__Payment__80EB26ED9E92DFD1");

            entity.ToTable("Payment");

            entity.HasIndex(e => e.ClickTransId, "UQ__Payment__80EB26ECF00931D4").IsUnique();

            entity.Property(e => e.ClickTransId).HasColumnName("click_trans_id");
            entity.Property(e => e.Action).HasColumnName("action");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.ClickPaydocId).HasColumnName("click_paydoc_id");
            entity.Property(e => e.Error).HasColumnName("error");
            entity.Property(e => e.ErrorNote)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("error_note");
            entity.Property(e => e.MerchantTransId)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("merchant_trans_id");
            entity.Property(e => e.ServiceId).HasColumnName("service_id");
            entity.Property(e => e.SignString)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("sign_string");
            entity.Property(e => e.SignTime)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("sign_time");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Payments)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Payment__user_id__7D439ABD");
        });

        modelBuilder.Entity<Photo>(entity =>
        {
            entity.HasKey(e => e.PhotoId).HasName("PK__Photo__CB48C83D17D6F1B1");

            entity.ToTable("Photo");

            entity.HasIndex(e => e.PhotoId, "UQ__Photo__CB48C83CEA866E3D").IsUnique();

            entity.Property(e => e.PhotoId).HasColumnName("photo_id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.PhotoPath)
                .HasMaxLength(500)
                .IsUnicode()
                .HasColumnName("photo_path");

            entity.HasOne(d => d.Apartment).WithMany(p => p.Photos)
                .HasForeignKey(d => d.ApartmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Photo__apartment__74AE54BC");
        });

        modelBuilder.Entity<ReviewApartment>(entity =>
        {
            entity.HasKey(e => e.RApartmentId).HasName("PK__Review A__4F55CBAA3BAE45B3");

            entity.ToTable("Review Apartment");

            entity.HasIndex(e => e.RApartmentId, "UQ__Review A__4F55CBABF9255E8D").IsUnique();

            entity.Property(e => e.RApartmentId).HasColumnName("r_apartment_id");
            entity.Property(e => e.ApartmentId).HasColumnName("apartment_id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Rating).HasColumnName("rating");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Apartment).WithMany(p => p.ReviewApartments)
                .HasForeignKey(d => d.ApartmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK__Review Ap__apart__07C12930");

            entity.HasOne(d => d.User).WithMany(p => p.ReviewApartments)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Review Ap__user___06CD04F7");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__User__B9BE370F33A45A75");

            entity.ToTable("User");

            entity.HasIndex(e => e.UserId, "UQ__User__B9BE370E4B593BC0").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.BirthDate).HasColumnName("birth_date");
            entity.Property(e => e.FirstName)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("first_name");
            entity.Property(e => e.IsLandlord).HasColumnName("isLandlord");
            entity.Property(e => e.IsTenant).HasColumnName("isTenant");
            entity.Property(e => e.LastName)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("last_name");
            entity.Property(e => e.Login)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("login");
            entity.Property(e => e.MiddleName)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("middle_name");
            entity.Property(e => e.PassportNumber)
                .HasMaxLength(7)
                .IsUnicode()
                .HasColumnName("passport_number");
            entity.Property(e => e.PassportSeries)
                .HasMaxLength(2)
                .IsUnicode()
                .IsFixedLength()
                .HasColumnName("passport_series");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("password");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(255)
                .IsUnicode()
                .HasColumnName("phone_number");
            entity.Property(e => e.Pinfl)
                .HasMaxLength(14)
                .IsUnicode()
                .HasColumnName("pinfl");
        });

        modelBuilder.Entity<UserLoginLog>(entity =>
        {
            entity.HasKey(e => e.LogId).HasName("PK__User log__9E2397E0C76C83BC");

            entity.ToTable("User login log");

            entity.HasIndex(e => e.LogId, "UQ__User log__9E2397E1D39DF833").IsUnique();

            entity.Property(e => e.LogId).HasColumnName("log_id");
            entity.Property(e => e.LoginTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("login_time");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.UserLoginLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__User logi__user___02084FDA");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
