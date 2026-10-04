using System.ComponentModel.DataAnnotations;

namespace TableItShared.Models;

public class MenuItem
{
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Name { get; set; } = "";
    [MaxLength(500)]
    public string Description { get; set; } = "";
    [Required, MaxLength(50)]
    public string Category { get; set; } = "";
    [Range(typeof(decimal), "0", "100000")]
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
}
