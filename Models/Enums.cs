namespace DevDen.Models;

public enum StatusCategory
{
    Backlog,
    Active,
    Done
}

public enum ItemType
{
    Task,
    Bug,
    Feature,
    Chore
}

public enum Priority
{
    P0,
    P1,
    P2,
    P3
}

public enum MilestoneState
{
    Open,
    Closed
}

public enum ActivityKind
{
    Created,
    StatusChanged,
    PriorityChanged,
    Edited,
    Completed
}

public enum ImportMode
{
    Replace,
    Merge
}
