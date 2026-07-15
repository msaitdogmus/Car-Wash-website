using System.ComponentModel.DataAnnotations;

namespace DryCar.Models;

public sealed class RegisterViewModel
{
    [Required, StringLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string LastName { get; set; } = string.Empty;

    [Required, Phone, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), MinLength(10), StringLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FaceBase64 { get; set; } = string.Empty;
}
