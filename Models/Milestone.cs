namespace DevDen.Models;

public record Milestone(
    Guid Id,
    Guid ProjectId,
    string Name,
    DateOnly? TargetDate,
    MilestoneState State
);
