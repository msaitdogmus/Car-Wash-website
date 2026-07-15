using System.ComponentModel.DataAnnotations;

namespace DryCar.Models;

public class Service
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "Hizmet adı zorunludur.")]
    [StringLength(100, ErrorMessage = "Hizmet adı en fazla 100 karakter olabilir.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Açıklama zorunludur.")]
    [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
    public string Description { get; set; } = string.Empty;

    [Range(0.0, 120000.0, ErrorMessage = "Fiyat 0 ile 120.000 arasında olmalıdır.")]
    [DataType(DataType.Currency)]
    public decimal Price { get; set; }
}
