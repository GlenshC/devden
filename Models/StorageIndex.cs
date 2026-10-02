namespace DevDen.Models;

public record StorageIndex(
    int Version,
    List<Guid> ProjectIds,
    UiPrefs Prefs
)
{
    public const int CurrentVersion = 1;

    public static StorageIndex CreateDefault() => new(
        Version: CurrentVersion,
        ProjectIds: new(),
        Prefs: UiPrefs.CreateDefault()
    );
}

public record StoreSnapshot(
    int Version,
    List<Project> Projects,
    Dictionary<Guid, List<Item>> Items,
    Dictionary<Guid, List<Milestone>> Milestones,
    Dictionary<Guid, List<ActivityEntry>> Activity,
    UiPrefs Prefs
);
