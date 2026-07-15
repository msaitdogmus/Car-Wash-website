using System.ComponentModel.DataAnnotations;

namespace DryCar.Models;

public class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; }

    [Required]
    [MinLength(6)]
    [DataType(DataType.Password)]
    public string Password { get; set; }

    [Required]
    [Compare("Password")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; }
}
