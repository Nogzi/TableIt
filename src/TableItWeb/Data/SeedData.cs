using TableItShared.Models;

namespace TableItWeb.Data;

public static class SeedData
{
    public static void Seed(TableItDbContext db)
    {
        if (db.Tables.Any())
            return;

        db.Tables.AddRange(
            new Table { Number = 1, Seats = 2, Shape = TableShape.Round, X = 150, Y = 150, Width = 90, Height = 90 },
            new Table { Number = 2, Seats = 4, Shape = TableShape.Round, X = 350, Y = 150, Width = 90, Height = 90 },
            new Table { Number = 3, Seats = 4, Shape = TableShape.Rect, X = 600, Y = 150, Width = 160, Height = 90 },
            new Table { Number = 4, Seats = 6, Shape = TableShape.Rect, X = 850, Y = 150, Width = 160, Height = 90 },
            new Table { Number = 5, Seats = 3, Shape = TableShape.Round, X = 200, Y = 450, Width = 90, Height = 90 },
            new Table { Number = 6, Seats = 6, Shape = TableShape.Rect, X = 550, Y = 450, Width = 160, Height = 90 });

        if (!db.MenuItems.Any())
        {
            db.MenuItems.AddRange(
                Item("Starters", "Marinated Herring", "Pickled herring with capers, red onion and rye bread", 85m),
                Item("Starters", "Shrimp on Toast", "Danish shrimp, mayonnaise, dill and lemon on buttered toast", 105m),
                Item("Starters", "Beetroot Salad", "Roasted beetroot, goat cheese and hazelnuts", 79m),
                Item("Mains", "Pan-fried Plaice", "Plaice with parsley sauce, boiled potatoes and lemon", 195m),
                Item("Mains", "Frikadeller", "Danish pork meatballs with red cabbage and potatoes", 165m),
                Item("Mains", "Stegt Flaesk", "Crispy pork belly with parsley sauce and new potatoes", 175m),
                Item("Desserts", "Apple Cake", "Layered apple cake with whipped cream", 75m),
                Item("Desserts", "Rice Pudding", "Risalamande with warm cherry sauce", 69m),
                Item("Drinks", "Tuborg Classic", "Draught beer, 40 cl", 55m),
                Item("Drinks", "Coffee", "Freshly brewed filter coffee", 35m));
        }

        db.SaveChanges();
    }

    private static MenuItem Item(string category, string name, string description, decimal price) =>
        new() { Category = category, Name = name, Description = description, Price = price, IsAvailable = true };
}
