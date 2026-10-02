using System.Text.Json;
using DevDen.Models;
using Microsoft.JSInterop;

namespace DevDen.Services;

public class LocalStorageDataStore : IDataStore
{
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private const string IndexKey = "den:v1:index";
    private static string ProjectKey(Guid id) => $"den:v1:project:{id}";
    private static string ItemsKey(Guid projectId) => $"den:v1:items:{projectId}";
    private static string MilestonesKey(Guid projectId) => $"den:v1:milestones:{projectId}";
    private static string ActivityKey(Guid projectId) => $"den:v1:activity:{projectId}";

    public LocalStorageDataStore(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<StoreSnapshot> LoadAllAsync()
    {
        try
        {
            var indexJson = await _js.InvokeAsync<string?>("denStorage.getItem", IndexKey);
            StorageIndex? index = null;
            if (!string.IsNullOrWhiteSpace(indexJson))
            {
                index = JsonSerializer.Deserialize<StorageIndex>(indexJson, JsonOptions);
            }

            if (index == null || index.ProjectIds.Count == 0)
            {
                // Create and return default seed store snapshot
                return CreateDefaultSeedSnapshot();
            }

            var projects = new List<Project>();
            var itemsMap = new Dictionary<Guid, List<Item>>();
            var milestonesMap = new Dictionary<Guid, List<Milestone>>();
            var activityMap = new Dictionary<Guid, List<ActivityEntry>>();

            foreach (var pid in index.ProjectIds)
            {
                var pJson = await _js.InvokeAsync<string?>("denStorage.getItem", ProjectKey(pid));
                if (!string.IsNullOrWhiteSpace(pJson))
                {
                    var project = JsonSerializer.Deserialize<Project>(pJson, JsonOptions);
                    if (project != null)
                    {
                        projects.Add(project);
                    }
                }

                var iJson = await _js.InvokeAsync<string?>("denStorage.getItem", ItemsKey(pid));
                if (!string.IsNullOrWhiteSpace(iJson))
                {
                    var items = JsonSerializer.Deserialize<List<Item>>(iJson, JsonOptions);
                    itemsMap[pid] = items ?? new List<Item>();
                }
                else
                {
                    itemsMap[pid] = new List<Item>();
                }

                var mJson = await _js.InvokeAsync<string?>("denStorage.getItem", MilestonesKey(pid));
                if (!string.IsNullOrWhiteSpace(mJson))
                {
                    var milestones = JsonSerializer.Deserialize<List<Milestone>>(mJson, JsonOptions);
                    milestonesMap[pid] = milestones ?? new List<Milestone>();
                }
                else
                {
                    milestonesMap[pid] = new List<Milestone>();
                }

                var aJson = await _js.InvokeAsync<string?>("denStorage.getItem", ActivityKey(pid));
                if (!string.IsNullOrWhiteSpace(aJson))
                {
                    var activity = JsonSerializer.Deserialize<List<ActivityEntry>>(aJson, JsonOptions);
                    activityMap[pid] = activity ?? new List<ActivityEntry>();
                }
                else
                {
                    activityMap[pid] = new List<ActivityEntry>();
                }
            }

            if (projects.Count == 0)
            {
                return CreateDefaultSeedSnapshot();
            }

            return new StoreSnapshot(
                Version: index.Version,
                Projects: projects,
                Items: itemsMap,
                Milestones: milestonesMap,
                Activity: activityMap,
                Prefs: index.Prefs ?? UiPrefs.CreateDefault()
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading from localStorage: {ex.Message}");
            return CreateDefaultSeedSnapshot();
        }
    }

    public async Task SaveIndexAsync(StorageIndex index)
    {
        var json = JsonSerializer.Serialize(index, JsonOptions);
        await _js.InvokeVoidAsync("denStorage.setItem", IndexKey, json);
    }

    public async Task SaveProjectAsync(Project p)
    {
        var json = JsonSerializer.Serialize(p, JsonOptions);
        await _js.InvokeVoidAsync("denStorage.setItem", ProjectKey(p.Id), json);
    }

    public async Task SaveItemsAsync(Guid projectId, IReadOnlyList<Item> items)
    {
        var json = JsonSerializer.Serialize(items, JsonOptions);
        await _js.InvokeVoidAsync("denStorage.setItem", ItemsKey(projectId), json);
    }

    public async Task SaveMilestonesAsync(Guid projectId, IReadOnlyList<Milestone> milestones)
    {
        var json = JsonSerializer.Serialize(milestones, JsonOptions);
        await _js.InvokeVoidAsync("denStorage.setItem", MilestonesKey(projectId), json);
    }

    public async Task SaveActivityAsync(Guid projectId, IReadOnlyList<ActivityEntry> log)
    {
        // Activity log capped at 200 per item
        var capped = log.TakeLast(500).ToList();
        var json = JsonSerializer.Serialize(capped, JsonOptions);
        await _js.InvokeVoidAsync("denStorage.setItem", ActivityKey(projectId), json);
    }

    public async Task DeleteProjectAsync(Guid projectId)
    {
        await _js.InvokeVoidAsync("denStorage.removeItem", ProjectKey(projectId));
        await _js.InvokeVoidAsync("denStorage.removeItem", ItemsKey(projectId));
        await _js.InvokeVoidAsync("denStorage.removeItem", MilestonesKey(projectId));
        await _js.InvokeVoidAsync("denStorage.removeItem", ActivityKey(projectId));
    }

    public async Task<string> ExportAsync()
    {
        var snapshot = await LoadAllAsync();
        return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task ImportAsync(string json, ImportMode mode)
    {
        var imported = JsonSerializer.Deserialize<StoreSnapshot>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid backup JSON payload.");

        if (mode == ImportMode.Replace)
        {
            await _js.InvokeVoidAsync("denStorage.clear");

            var index = new StorageIndex(
                Version: StorageIndex.CurrentVersion,
                ProjectIds: imported.Projects.Select(p => p.Id).ToList(),
                Prefs: imported.Prefs ?? UiPrefs.CreateDefault()
            );

            await SaveIndexAsync(index);

            foreach (var p in imported.Projects)
            {
                await SaveProjectAsync(p);
                if (imported.Items.TryGetValue(p.Id, out var items))
                {
                    await SaveItemsAsync(p.Id, items);
                }
                if (imported.Milestones.TryGetValue(p.Id, out var milestones))
                {
                    await SaveMilestonesAsync(p.Id, milestones);
                }
                if (imported.Activity.TryGetValue(p.Id, out var act))
                {
                    await SaveActivityAsync(p.Id, act);
                }
            }
        }
        else // Merge mode
        {
            var existing = await LoadAllAsync();
            var existingProjectIds = new HashSet<Guid>(existing.Projects.Select(p => p.Id));
            var existingKeys = new HashSet<string>(existing.Projects.Select(p => p.Key), StringComparer.OrdinalIgnoreCase);

            foreach (var p in imported.Projects)
            {
                var projectToSave = p;
                if (existingKeys.Contains(p.Key) && !existingProjectIds.Contains(p.Id))
                {
                    // Generate new non-colliding key
                    var newKey = (p.Key.Length < 5 ? p.Key + "1" : p.Key[..4] + "1").ToUpperInvariant();
                    projectToSave = p with { Key = newKey };
                }

                await SaveProjectAsync(projectToSave);

                if (imported.Items.TryGetValue(p.Id, out var items))
                {
                    await SaveItemsAsync(p.Id, items);
                }
                if (imported.Milestones.TryGetValue(p.Id, out var milestones))
                {
                    await SaveMilestonesAsync(p.Id, milestones);
                }
                if (imported.Activity.TryGetValue(p.Id, out var act))
                {
                    await SaveActivityAsync(p.Id, act);
                }

                existingProjectIds.Add(projectToSave.Id);
            }

            var updatedIndex = new StorageIndex(
                Version: StorageIndex.CurrentVersion,
                ProjectIds: existingProjectIds.ToList(),
                Prefs: existing.Prefs
            );
            await SaveIndexAsync(updatedIndex);
        }
    }

    private static StoreSnapshot CreateDefaultSeedSnapshot()
    {
        var projectId = Guid.NewGuid();
        var defaultProject = Project.CreateDefault("DevDen Core", "DEN");
        defaultProject = defaultProject with { Id = projectId, NextItemNumber = 7 };

        var backlog = defaultProject.Statuses.First(s => s.Name == "Backlog");
        var todo = defaultProject.Statuses.First(s => s.Name == "Todo");
        var inProgress = defaultProject.Statuses.First(s => s.Name == "In Progress");
        var review = defaultProject.Statuses.First(s => s.Name == "Review");
        var done = defaultProject.Statuses.First(s => s.Name == "Done");

        var coreLabel = defaultProject.Labels.First(l => l.Name == "Core");
        var uiLabel = defaultProject.Labels.First(l => l.Name == "UI");
        var perfLabel = defaultProject.Labels.First(l => l.Name == "Perf");

        var m1 = new Milestone(Guid.NewGuid(), projectId, "v0.1 Public Alpha", DateOnly.FromDateTime(DateTime.Today.AddDays(14)), MilestoneState.Open);
        var m2 = new Milestone(Guid.NewGuid(), projectId, "v0.2 Native Interop", DateOnly.FromDateTime(DateTime.Today.AddDays(45)), MilestoneState.Open);

        var items = new List<Item>
        {
            new(
                Id: Guid.NewGuid(),
                ProjectId: projectId,
                Number: 1,
                Type: ItemType.Feature,
                Title: "Implement keyboard-driven Command Bar (Ctrl+K)",
                Body: "Build a sleek dark-glass launcher with fuzzy search over commands, items, and projects.\n\n### Requirements\n- Intercept `Ctrl/Cmd+K` globally\n- `#` prefix searches items\n- `@` prefix switches projects\n- Keyboard arrow navigation and Enter to dispatch",
                StatusId: done.Id,
                Order: 1024.0,
                Priority: Priority.P0,
                LabelIds: new() { uiLabel.Id, coreLabel.Id },
                MilestoneId: m1.Id,
                Checklist: new()
                {
                    new(Guid.NewGuid(), "JS keydown capture", true, 100),
                    new(Guid.NewGuid(), "Fuzzy title match", true, 200),
                    new(Guid.NewGuid(), "Pill badge indicators", true, 300)
                },
                DueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
                InToday: false,
                CreatedAt: DateTime.UtcNow.AddDays(-5),
                UpdatedAt: DateTime.UtcNow.AddDays(-1),
                CompletedAt: DateTime.UtcNow.AddDays(-1)
            ),
            new(
                Id: Guid.NewGuid(),
                ProjectId: projectId,
                Number: 2,
                Type: ItemType.Feature,
                Title: "Kanban board with HTML5 drag and drop",
                Body: "Native drag-and-drop cards with smooth drop indicator line and fractional ordering algorithm.",
                StatusId: review.Id,
                Order: 1024.0,
                Priority: Priority.P1,
                LabelIds: new() { uiLabel.Id },
                MilestoneId: m1.Id,
                Checklist: new()
                {
                    new(Guid.NewGuid(), "HTML5 drag handlers", true, 100),
                    new(Guid.NewGuid(), "Drop target calculation", true, 200),
                    new(Guid.NewGuid(), "Optimistic order update", true, 300)
                },
                DueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
                InToday: true,
                CreatedAt: DateTime.UtcNow.AddDays(-4),
                UpdatedAt: DateTime.UtcNow.AddHours(-2),
                CompletedAt: null
            ),
            new(
                Id: Guid.NewGuid(),
                ProjectId: projectId,
                Number: 3,
                Type: ItemType.Task,
                Title: "Ambient Aurora background with film grain SVG filter",
                Body: "Design 4 blade divs animated on staggered 15-22s loops with crimson/coral/amber gradients.",
                StatusId: inProgress.Id,
                Order: 1024.0,
                Priority: Priority.P2,
                LabelIds: new() { uiLabel.Id },
                MilestoneId: m1.Id,
                Checklist: new()
                {
                    new(Guid.NewGuid(), "feTurbulence SVG grain filter", true, 100),
                    new(Guid.NewGuid(), "Keyframe rotation/scale animation", true, 200),
                    new(Guid.NewGuid(), "Route opacity attenuation (25% on workspace)", false, 300)
                },
                DueDate: DateOnly.FromDateTime(DateTime.Today),
                InToday: true,
                CreatedAt: DateTime.UtcNow.AddDays(-3),
                UpdatedAt: DateTime.UtcNow.AddMinutes(-30),
                CompletedAt: null
            ),
            new(
                Id: Guid.NewGuid(),
                ProjectId: projectId,
                Number: 4,
                Type: ItemType.Bug,
                Title: "Fix fractional ordering collision on repeated rapid drags",
                Body: "When delta between adjacent items falls below `1e-6`, rebalance column order with 1024 step increments.",
                StatusId: todo.Id,
                Order: 1024.0,
                Priority: Priority.P0,
                LabelIds: new() { coreLabel.Id, perfLabel.Id },
                MilestoneId: m1.Id,
                Checklist: new(),
                DueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
                InToday: false,
                CreatedAt: DateTime.UtcNow.AddDays(-2),
                UpdatedAt: DateTime.UtcNow.AddDays(-2),
                CompletedAt: null
            ),
            new(
                Id: Guid.NewGuid(),
                ProjectId: projectId,
                Number: 5,
                Type: ItemType.Feature,
                Title: "Cross-project Today focus view with quick-add parser",
                Body: "Aggregate all `InToday` flagged tasks with `#KEY` tag parsing for instant project targeting.",
                StatusId: todo.Id,
                Order: 2048.0,
                Priority: Priority.P2,
                LabelIds: new() { uiLabel.Id },
                MilestoneId: m1.Id,
                Checklist: new(),
                DueDate: DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
                InToday: false,
                CreatedAt: DateTime.UtcNow.AddDays(-1),
                UpdatedAt: DateTime.UtcNow.AddDays(-1),
                CompletedAt: null
            ),
            new(
                Id: Guid.NewGuid(),
                ProjectId: projectId,
                Number: 6,
                Type: ItemType.Chore,
                Title: "Write simulated commit history scripts for GitHub portfolio",
                Body: "Provide both PowerShell and Bash automated git commit progression.",
                StatusId: backlog.Id,
                Order: 1024.0,
                Priority: Priority.P3,
                LabelIds: new() { coreLabel.Id },
                MilestoneId: m2.Id,
                Checklist: new(),
                DueDate: null,
                InToday: false,
                CreatedAt: DateTime.UtcNow.AddHours(-10),
                UpdatedAt: DateTime.UtcNow.AddHours(-10),
                CompletedAt: null
            )
        };

        var activities = new List<ActivityEntry>
        {
            new(Guid.NewGuid(), items[0].Id, ActivityKind.Created, null, "Item created", DateTime.UtcNow.AddDays(-5)),
            new(Guid.NewGuid(), items[0].Id, ActivityKind.StatusChanged, "Review", "Done", DateTime.UtcNow.AddDays(-1)),
            new(Guid.NewGuid(), items[1].Id, ActivityKind.Created, null, "Item created", DateTime.UtcNow.AddDays(-4)),
            new(Guid.NewGuid(), items[1].Id, ActivityKind.StatusChanged, "In Progress", "Review", DateTime.UtcNow.AddHours(-2)),
            new(Guid.NewGuid(), items[2].Id, ActivityKind.Created, null, "Item created", DateTime.UtcNow.AddDays(-3)),
            new(Guid.NewGuid(), items[2].Id, ActivityKind.StatusChanged, "Todo", "In Progress", DateTime.UtcNow.AddMinutes(-30))
        };

        return new StoreSnapshot(
            Version: StorageIndex.CurrentVersion,
            Projects: new List<Project> { defaultProject },
            Items: new Dictionary<Guid, List<Item>> { [projectId] = items },
            Milestones: new Dictionary<Guid, List<Milestone>> { [projectId] = new() { m1, m2 } },
            Activity: new Dictionary<Guid, List<ActivityEntry>> { [projectId] = activities },
            Prefs: UiPrefs.CreateDefault() with { ActiveProjectId = projectId }
        );
    }
}
