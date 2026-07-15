using System;
using DryCar.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DryCar.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20250627134924_AddPasswordResetFieldsToUser")]
public class AddPasswordResetFieldsToUser : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("PasswordResetToken", "Users", "nvarchar(max)", null, null, rowVersion: false, null, nullable: true);
        migrationBuilder.AddColumn<DateTime>("PasswordResetTokenExpiry", "Users", "datetime2", null, null, rowVersion: false, null, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("PasswordResetToken", "Users");
        migrationBuilder.DropColumn("PasswordResetTokenExpiry", "Users");
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
            b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
            b.Property<bool>("IsArchived").HasColumnType("bit");
            b.Property<bool>("IsConfirmed").HasColumnType("bit");
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
            b.Property<string>("Description").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<string>("Name").IsRequired().HasColumnType("nvarchar(max)");
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
            b.Property<string>("FirstName").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<bool>("HasFreeDeal").HasColumnType("bit");
            b.Property<string>("LastName").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<string>("PasswordHash").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<string>("PasswordResetToken").HasColumnType("nvarchar(max)");
            b.Property<DateTime?>("PasswordResetTokenExpiry").HasColumnType("datetime2");
            b.Property<string>("Phone").IsRequired().HasColumnType("nvarchar(max)");
            b.HasKey("Id");
            b.ToTable("Users");
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
        modelBuilder.Entity("DryCar.Models.User", delegate (EntityTypeBuilder b)
        {
            b.Navigation("Appointments");
        });
    }
}
