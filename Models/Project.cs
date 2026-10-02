namespace DevDen.Models;

public record Project(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    List<Status> Statuses,
    List<Label> Labels,
    int NextItemNumber,
    DateTime CreatedAt,
    DateTime? ArchivedAt
)
{
    public static Project CreateDefault(string name, string key)
    {
        var id = Guid.NewGuid();
        var statuses = new List<Status>
        {
            new(Guid.NewGuid(), "Backlog", StatusCategory.Backlog, 1024.0),
            new(Guid.NewGuid(), "Todo", StatusCategory.Active, 2048.0),
            new(Guid.NewGuid(), "In Progress", StatusCategory.Active, 3072.0),
            new(Guid.NewGuid(), "Review", StatusCategory.Active, 4096.0),
            new(Guid.NewGuid(), "Done", StatusCategory.Done, 5120.0)
        };

        var labels = new List<Label>
        {
            new(Guid.NewGuid(), "Core", "crimson"),
            new(Guid.NewGuid(), "UI", "coral"),
            new(Guid.NewGuid(), "Perf", "amber"),
            new(Guid.NewGuid(), "Docs", "ash")
        };

        return new Project(
            Id: id,
            Key: key.ToUpperInvariant().Trim(),
            Name: name.Trim(),
            Description: null,
            Statuses: statuses,
            Labels: labels,
            NextItemNumber: 1,
            CreatedAt: DateTime.UtcNow,
            ArchivedAt: null
        );
    }
}
