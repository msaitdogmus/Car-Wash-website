using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DryCar.Models;

public class Appointment
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    public int ServiceId { get; set; }

    public DateTime AppointmentDate { get; set; }

    [BindNever]
    public virtual User? User { get; set; }

    [BindNever]
    public virtual Service? Service { get; set; }

    public bool IsConfirmed { get; set; }

    public bool IsFreeDealApplied { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsArchived { get; set; }
}
