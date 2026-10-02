namespace DevDen.Models;

public record UiPrefs(
    Guid? ActiveProjectId,
    string ViewMode,      // "board", "list", "roadmap"
    string? GroupBy,      // null, "status", "priority", "milestone", "type"
    string? SortBy,       // "number", "title", "priority", "status", "due", "updated"
    bool SortDescending,
    string? SearchQuery,
    List<string> FilterTypes,
    List<Guid> FilterStatusIds,
    List<Priority> FilterPriorities,
    List<Guid> FilterLabelIds
)
{
    public static UiPrefs CreateDefault() => new(
        ActiveProjectId: null,
        ViewMode: "board",
        GroupBy: null,
        SortBy: "number",
        SortDescending: false,
        SearchQuery: null,
        FilterTypes: new(),
        FilterStatusIds: new(),
        FilterPriorities: new(),
        FilterLabelIds: new()
    );
}
