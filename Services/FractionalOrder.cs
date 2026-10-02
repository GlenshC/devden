using DevDen.Models;

namespace DevDen.Services;

public static class FractionalOrder
{
    public const double Step = 1024.0;
    public const double Epsilon = 1e-6;

    public static double CalculateBetween(double prev, double next)
    {
        return (prev + next) / 2.0;
    }

    public static double CalculateBefore(double firstOrder)
    {
        return firstOrder - Step;
    }

    public static double CalculateAfter(double lastOrder)
    {
        return lastOrder + Step;
    }

    public static bool NeedsRebalance(double a, double b)
    {
        return Math.Abs(a - b) < Epsilon;
    }

    public static List<Item> RebalanceColumn(IEnumerable<Item> columnItems)
    {
        var list = columnItems.OrderBy(i => i.Order).ToList();
        var result = new List<Item>();
        double current = Step;

        foreach (var item in list)
        {
            result.Add(item with { Order = current, UpdatedAt = DateTime.UtcNow });
            current += Step;
        }

        return result;
    }
}
