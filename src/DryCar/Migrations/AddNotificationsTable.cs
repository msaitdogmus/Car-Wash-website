using System;
using DryCar.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace DryCar.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20250911212045_AddNotificationsTable")]
public class AddNotificationsTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        int? maxLength = 100;
        Type typeFromHandle = typeof(string);
        migrationBuilder.AlterColumn<string>("Name", "Services", "nvarchar(100)", null, maxLength, rowVersion: false, null, nullable: false, null, null, null, typeFromHandle, "nvarchar(max)");
        maxLength = 500;
        typeFromHandle = typeof(string);
        migrationBuilder.AlterColumn<string>("Description", "Services", "nvarchar(500)", null, maxLength, rowVersion: false, null, nullable: false, null, null, null, typeFromHandle, "nvarchar(max)");
        migrationBuilder.CreateTable("Notifications", (ColumnsBuilder table) => new
        {
            Id = table.Column<int>("int").Annotation("SqlServer:Identity", "1, 1"),
            UserId = table.Column<int>("int"),
            Message = table.Column<string>("nvarchar(max)"),
            IsRead = table.Column<bool>("bit"),
            CreatedAt = table.Column<DateTime>("datetime2")
        }, null, table =>
        {
            table.PrimaryKey("PK_Notifications", x => x.Id);
            table.ForeignKey("FK_Notifications_Users_UserId", x => x.UserId, "Users", "Id", null, ReferentialAction.NoAction, ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("IX_Notifications_UserId", "Notifications", "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Notifications");
        Type typeFromHandle = typeof(string);
        int? oldMaxLength = 100;
        migrationBuilder.AlterColumn<string>("Name", "Services", "nvarchar(max)", null, null, rowVersion: false, null, nullable: false, null, null, null, typeFromHandle, "nvarchar(100)", null, oldMaxLength);
        typeFromHandle = typeof(string);
        oldMaxLength = 500;
        migrationBuilder.AlterColumn<string>("Description", "Services", "nvarchar(max)", null, null, rowVersion: false, null, nullable: false, null, null, null, typeFromHandle, "nvarchar(500)", null, oldMaxLength);
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
