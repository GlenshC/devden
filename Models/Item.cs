namespace DevDen.Models;

public record Item(
    Guid Id,
    Guid ProjectId,
    int Number,
    ItemType Type,
    string Title,
    string? Body,
    Guid StatusId,
    double Order,
    Priority Priority,
    List<Guid> LabelIds,
    Guid? MilestoneId,
    List<ChecklistEntry> Checklist,
    DateOnly? DueDate,
    bool InToday,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt
)
{
    public double Progress(bool isDone) =>
        Checklist.Count == 0
            ? (isDone ? 1.0 : 0.0)
            : (Checklist.Count(c => c.Done) / (double)Checklist.Count);

    public int CompletedChecklistCount => Checklist.Count(c => c.Done);
    public int TotalChecklistCount => Checklist.Count;
    public bool HasChecklist => Checklist.Count > 0;
}
