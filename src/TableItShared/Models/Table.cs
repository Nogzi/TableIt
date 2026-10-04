using System.ComponentModel.DataAnnotations;

namespace TableItShared.Models;

public enum TableShape
{
    Round,
    Rect
}

/// <summary>
/// A table on the floor plan. X and Y are the table's centre in a 1000x700 logical canvas.
/// Rotation is in degrees.
/// </summary>
public class Table
{
    public int Id { get; set; }
    [Range(1, 999)]
    public int Number { get; set; }
    [Range(1, 50)]
    public int Seats { get; set; }
    public TableShape Shape { get; set; }
    [Range(0, 1000)]
    public double X { get; set; }
    [Range(0, 700)]
    public double Y { get; set; }
    [Range(20, 1000)]
    public double Width { get; set; }
    [Range(20, 1000)]
    public double Height { get; set; }
    [Range(0, 360)]
    public double Rotation { get; set; }
}
