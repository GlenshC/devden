using DevDen.Models;

namespace DevDen.Services;

public class AppState
{
    private readonly DebouncedPersister _persister;

    public bool IsInitialized { get; private set; }
    public List<Project> Projects { get; private set; } = new();
    public Guid? ActiveProjectId { get; private set; }
    public Dictionary<Guid, List<Item>> ItemsByProject { get; private set; } = new();
    public Dictionary<Guid, List<Milestone>> MilestonesByProject { get; private set; } = new();
    public Dictionary<Guid, List<ActivityEntry>> ActivityByProject { get; private set; } = new();
    public UiPrefs Prefs { get; private set; } = UiPrefs.CreateDefault();

    // UI Local State managed globally
    public Item? SelectedItem { get; set; }
    public bool IsDetailOpen => SelectedItem != null;
    public bool IsCommandBarOpen { get; set; }
    public string CurrentRoute { get; set; } = "/";

    public event Action? OnChange;

    public AppState(IDataStore store)
    {
        _persister = new DebouncedPersister(store, () => this);
    }

    public Project? ActiveProject =>
        Projects.FirstOrDefault(p => p.Id == ActiveProjectId) ?? Projects.FirstOrDefault();

    public IReadOnlyList<Item> ActiveItems =>
        ActiveProjectId.HasValue && ItemsByProject.TryGetValue(ActiveProjectId.Value, out var items)
            ? items
            : Array.Empty<Item>();

    public IReadOnlyList<Milestone> ActiveMilestones =>
        ActiveProjectId.HasValue && MilestonesByProject.TryGetValue(ActiveProjectId.Value, out var m)
            ? m
            : Array.Empty<Milestone>();

    public IReadOnlyList<Status> ActiveStatuses =>
        ActiveProject?.Statuses ?? (IReadOnlyList<Status>)Array.Empty<Status>();

    public IReadOnlyList<Label> ActiveLabels =>
        ActiveProject?.Labels ?? (IReadOnlyList<Label>)Array.Empty<Label>();

    public IReadOnlyList<Item> AllTodayItems =>
        ItemsByProject.Values.SelectMany(list => list).Where(i => i.InToday).ToList();

    public void Initialize(StoreSnapshot snapshot)
    {
        Projects = snapshot.Projects;
        ItemsByProject = snapshot.Items;
        MilestonesByProject = snapshot.Milestones;
        ActivityByProject = snapshot.Activity;
        Prefs = snapshot.Prefs ?? UiPrefs.CreateDefault();

        if (Prefs.ActiveProjectId.HasValue && Projects.Any(p => p.Id == Prefs.ActiveProjectId.Value))
        {
            ActiveProjectId = Prefs.ActiveProjectId.Value;
        }
        else
        {
            ActiveProjectId = Projects.FirstOrDefault()?.Id;
        }

        IsInitialized = true;
        NotifyStateChanged();
    }

    public void SetActiveProject(Guid projectId)
    {
        if (ActiveProjectId != projectId && Projects.Any(p => p.Id == projectId))
        {
            ActiveProjectId = projectId;
            Prefs = Prefs with { ActiveProjectId = projectId };
            _persister.SchedulePersist();
            NotifyStateChanged();
        }
    }

    public void SetActiveProjectByKey(string key)
    {
        var proj = Projects.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (proj != null && proj.Id != ActiveProjectId)
        {
            ActiveProjectId = proj.Id;
            Prefs = Prefs with { ActiveProjectId = proj.Id };
            _persister.SchedulePersist();
            NotifyStateChanged();
        }
    }

    public void UpdatePrefs(UiPrefs newPrefs)
    {
        Prefs = newPrefs;
        _persister.SchedulePersist();
        NotifyStateChanged();
    }

    public void SetSelectedItem(Item? item)
    {
        SelectedItem = item;
        NotifyStateChanged();
    }

    public void CloseDetail()
    {
        SelectedItem = null;
        NotifyStateChanged();
    }

    public void ToggleCommandBar(bool? open = null)
    {
        IsCommandBarOpen = open ?? !IsCommandBarOpen;
        NotifyStateChanged();
    }

    public void SchedulePersist()
    {
        _persister.SchedulePersist();
    }

    public async Task FlushPersistAsync()
    {
        await _persister.FlushAsync();
    }

    public void NotifyStateChanged() => OnChange?.Invoke();
}
