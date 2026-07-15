using System;
using System.Collections.Generic;

namespace DryCar.Models;

public class User
{
    public int Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? PasswordResetToken { get; set; }

    public DateTime? PasswordResetTokenExpiry { get; set; }

    public bool HasFreeDeal { get; set; }

    public string FaceVector { get; set; } = string.Empty;

    public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? FreeDealGrantedAt { get; set; }

    public DateTime? FreeDealExpiresAt { get; set; }

    public DateTime? FreeDealNotifiedAt { get; set; }

    public DateTime? FreeDealReminderSentAt { get; set; }

    public int PaidWashCount { get; set; }

    public DateTime? FreeDealRedeemedAt { get; set; }

    public int? FreeDealReservedAppointmentId { get; set; }

    public DateTime? FreeDealCycleStartAt { get; set; }

    public DateTime? FreeDealCycleEndAt { get; set; }

    public int PaidWashCountInCycle { get; set; }

    public int FreeDealsGrantedInCycle { get; set; }

    public int FreeDealBalance { get; set; }
}
