using System;
using DryCar.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DryCar.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260618114912_InitialFreshDb")]
public class InitialFreshDb : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "9.0.3").HasAnnotation("Relational:MaxIdentifierLength", 128);
        modelBuilder.UseIdentityColumns(1L);
        modelBuilder.Entity("DryCar.Models.Admin", delegate (EntityTypeBuilder b)
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
            b.Property<int>("Id").UseIdentityColumn(1L);
            b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
            b.Property<string>("PasswordHash").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<string>("Username").IsRequired().HasColumnType("nvarchar(max)");
            b.HasKey("Id");
            b.ToTable("Admins");
        });
        modelBuilder.Entity("DryCar.Models.Appointment", delegate (EntityTypeBuilder b)
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
            b.Property<int>("Id").UseIdentityColumn(1L);
            b.Property<DateTime>("AppointmentDate").HasColumnType("datetime2");
            b.Property<DateTime?>("ConfirmedAt").HasColumnType("datetime2");
            b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
            b.Property<bool>("IsArchived").HasColumnType("bit");
            b.Property<bool>("IsConfirmed").HasColumnType("bit");
            b.Property<bool>("IsFreeDealApplied").HasColumnType("bit");
            b.Property<int>("ServiceId").HasColumnType("int");
            b.Property<int>("UserId").HasColumnType("int");
            b.HasKey("Id");
            b.HasIndex("ServiceId");
            b.HasIndex("UserId");
            b.ToTable("Appointments");
        });
        modelBuilder.Entity("DryCar.Models.Service", delegate (EntityTypeBuilder b)
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
            b.Property<int>("Id").UseIdentityColumn(1L);
            b.Property<string>("Description").IsRequired().HasMaxLength(500)
                .HasColumnType("nvarchar(500)");
            b.Property<string>("Name").IsRequired().HasMaxLength(100)
                .HasColumnType("nvarchar(100)");
            b.Property<decimal>("Price").HasColumnType("decimal(18,2)");
            b.HasKey("Id");
            b.ToTable("Services");
        });
        modelBuilder.Entity("DryCar.Models.User", delegate (EntityTypeBuilder b)
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
            b.Property<int>("Id").UseIdentityColumn(1L);
            b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
            b.Property<string>("Email").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<string>("FaceVector").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<string>("FirstName").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<int>("FreeDealBalance").HasColumnType("int");
            b.Property<DateTime?>("FreeDealCycleEndAt").HasColumnType("datetime2");
            b.Property<DateTime?>("FreeDealCycleStartAt").HasColumnType("datetime2");
            b.Property<DateTime?>("FreeDealExpiresAt").HasColumnType("datetime2");
            b.Property<DateTime?>("FreeDealGrantedAt").HasColumnType("datetime2");
            b.Property<DateTime?>("FreeDealNotifiedAt").HasColumnType("datetime2");
            b.Property<DateTime?>("FreeDealRedeemedAt").HasColumnType("datetime2");
            b.Property<DateTime?>("FreeDealReminderSentAt").HasColumnType("datetime2");
            b.Property<int?>("FreeDealReservedAppointmentId").HasColumnType("int");
            b.Property<int>("FreeDealsGrantedInCycle").HasColumnType("int");
            b.Property<bool>("HasFreeDeal").HasColumnType("bit");
            b.Property<string>("LastName").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<int>("PaidWashCount").HasColumnType("int");
            b.Property<int>("PaidWashCountInCycle").HasColumnType("int");
            b.Property<string>("PasswordHash").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<string>("PasswordResetToken").HasColumnType("nvarchar(max)");
            b.Property<DateTime?>("PasswordResetTokenExpiry").HasColumnType("datetime2");
            b.Property<string>("Phone").IsRequired().HasColumnType("nvarchar(max)");
            b.HasKey("Id");
            b.ToTable("Users");
        });
        modelBuilder.Entity("Notification", delegate (EntityTypeBuilder b)
        {
            b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("int");
            b.Property<int>("Id").UseIdentityColumn(1L);
            b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
            b.Property<bool>("IsRead").HasColumnType("bit");
            b.Property<string>("Message").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<int>("UserId").HasColumnType("int");
            b.HasKey("Id");
            b.HasIndex("UserId");
            b.ToTable("Notifications");
        });
        modelBuilder.Entity("DryCar.Models.Appointment", delegate (EntityTypeBuilder b)
        {
            b.HasOne("DryCar.Models.Service", "Service").WithMany().HasForeignKey("ServiceId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            b.HasOne("DryCar.Models.User", "User").WithMany("Appointments").HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            b.Navigation("Service");
            b.Navigation("User");
        });
        modelBuilder.Entity("Notification", delegate (EntityTypeBuilder b)
        {
            b.HasOne("DryCar.Models.User", "User").WithMany().HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
            b.Navigation("User");
        });
        modelBuilder.Entity("DryCar.Models.User", delegate (EntityTypeBuilder b)
        {
            b.Navigation("Appointments");
        });
    }
}
