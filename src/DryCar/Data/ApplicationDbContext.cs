using DryCar.Models;
using Microsoft.EntityFrameworkCore;

namespace DryCar.Data;

public class ApplicationDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public DbSet<Admin> Admins { get; set; }

    public DbSet<Service> Services { get; set; }

    public DbSet<Appointment> Appointments { get; set; }

    public DbSet<Notification> Notifications { get; set; }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.Email).HasMaxLength(254);
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.PasswordResetToken).HasMaxLength(64);
        });

        modelBuilder.Entity<Admin>(entity =>
        {
            entity.Property(admin => admin.Username).HasMaxLength(100);
            entity.HasIndex(admin => admin.Username).IsUnique();
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasIndex(appointment => appointment.AppointmentDate);
            entity.HasIndex(appointment => new { appointment.ServiceId, appointment.AppointmentDate })
                .IsUnique()
                .HasFilter("[IsArchived] = 0");
        });
    }
}
