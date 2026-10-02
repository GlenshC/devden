namespace DevDen.Models;

public record ActivityEntry(
    Guid Id,
    Guid ItemId,
    ActivityKind Kind,
    string? From,
    string? To,
    DateTime At
);
