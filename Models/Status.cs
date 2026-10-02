namespace DevDen.Models;

public record Status(
    Guid Id,
    string Name,
    StatusCategory Category,
    double Order
);
