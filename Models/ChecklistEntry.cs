namespace DevDen.Models;

public record ChecklistEntry(
    Guid Id,
    string Text,
    bool Done,
    double Order
);
