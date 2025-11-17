using System.ComponentModel.DataAnnotations;

namespace BarPos.Models
{
    public class Contrasena
    {
        [Key]
        [MaxLength(50)]
        public string Clave { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Valor { get; set; } = string.Empty;
    }
}
