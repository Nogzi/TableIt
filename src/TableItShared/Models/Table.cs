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
    public int Number { get; set; }
    public int Seats { get; set; }
    public TableShape Shape { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }
}
