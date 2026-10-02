using System.Text.RegularExpressions;
using DevDen.Models;
using DevDen.Models.Common;

namespace DevDen.Services;

public class ProjectService
{
    private readonly AppState _state;
    private readonly ToastService _toast;

    private static readonly Regex KeyRegex = new("^[A-Z]{2,5}$", RegexOptions.Compiled);

    public ProjectService(AppState state, ToastService toast)
    {
        _state = state;
        _toast = toast;
    }

    public Result<Project> CreateProject(string name, string key)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        var trimmedKey = key?.Trim().ToUpperInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return Result<Project>.Fail("NameEmpty", "Project name cannot be empty.");
        }

        if (!KeyRegex.IsMatch(trimmedKey))
        {
            return Result<Project>.Fail("InvalidKey", "Key must be 2-5 uppercase letters (e.g. DEN).");
        }

        if (_state.Projects.Any(p => p.Key.Equals(trimmedKey, StringComparison.OrdinalIgnoreCase)))
        {
            return Result<Project>.Fail("KeyTaken", $"Project key '{trimmedKey}' is already in use.");
        }

        var project = Project.CreateDefault(trimmedName, trimmedKey);
        _state.Projects.Add(project);
        _state.ItemsByProject[project.Id] = new List<Item>();
        _state.MilestonesByProject[project.Id] = new List<Milestone>();
        _state.ActivityByProject[project.Id] = new List<ActivityEntry>();
        _state.SetActiveProject(project.Id);

        _state.SchedulePersist();
        _state.NotifyStateChanged();
        _toast.ShowSuccess($"Project {project.Name} ({project.Key}) created");

        return Result<Project>.Ok(project);
    }

    public Result<Project> UpdateProject(Guid id, string name, string? description)
    {
        var projIndex = _state.Projects.FindIndex(p => p.Id == id);
        if (projIndex < 0)
        {
            return Result<Project>.Fail("NotFound", "Project not found.");
        }

        var current = _state.Projects[projIndex];
        var updated = current with
        {
            Name = string.IsNullOrWhiteSpace(name) ? current.Name : name.Trim(),
            Description = description
        };

        _state.Projects[projIndex] = updated;
        _state.SchedulePersist();
        _state.NotifyStateChanged();

        return Result<Project>.Ok(updated);
    }

    public Result DeleteProject(Guid id)
    {
        var proj = _state.Projects.FirstOrDefault(p => p.Id == id);
        if (proj == null)
        {
            return Result.Fail("NotFound", "Project not found.");
        }

        if (_state.Projects.Count <= 1)
        {
            return Result.Fail("CannotDeleteLast", "Cannot delete the only remaining project.");
        }

        _state.Projects.RemoveAll(p => p.Id == id);
        _state.ItemsByProject.Remove(id);
        _state.MilestonesByProject.Remove(id);
        _state.ActivityByProject.Remove(id);

        if (_state.ActiveProjectId == id)
        {
            _state.SetActiveProject(_state.Projects.First().Id);
        }
        else
        {
            _state.SchedulePersist();
            _state.NotifyStateChanged();
        }

        _toast.ShowSuccess($"Project {proj.Name} deleted");
        return Result.Ok();
    }

    public Result<Status> AddStatus(Guid projectId, string name, StatusCategory category)
    {
        var projIndex = _state.Projects.FindIndex(p => p.Id == projectId);
        if (projIndex < 0) return Result<Status>.Fail("NotFound", "Project not found.");

        var project = _state.Projects[projIndex];
        var maxOrder = project.Statuses.Count > 0 ? project.Statuses.Max(s => s.Order) : 0.0;
        var newStatus = new Status(Guid.NewGuid(), name.Trim(), category, maxOrder + 1024.0);

        var updatedStatuses = new List<Status>(project.Statuses) { newStatus };
        _state.Projects[projIndex] = project with { Statuses = updatedStatuses };

        _state.SchedulePersist();
        _state.NotifyStateChanged();

        return Result<Status>.Ok(newStatus);
    }

    public Result DeleteStatus(Guid projectId, Guid statusId, Guid migrateToStatusId)
    {
        var projIndex = _state.Projects.FindIndex(p => p.Id == projectId);
        if (projIndex < 0) return Result.Fail("NotFound", "Project not found.");

        var project = _state.Projects[projIndex];
        var statusToDelete = project.Statuses.FirstOrDefault(s => s.Id == statusId);
        if (statusToDelete == null) return Result.Fail("NotFound", "Status not found.");

        // Rule: Rejected if it is the last status in its category
        var sameCategoryCount = project.Statuses.Count(s => s.Category == statusToDelete.Category);
        if (sameCategoryCount <= 1)
        {
            return Result.Fail("LastInCategory", $"Cannot delete the only status in the '{statusToDelete.Category}' category.");
        }

        var targetStatus = project.Statuses.FirstOrDefault(s => s.Id == migrateToStatusId);
        if (targetStatus == null)
        {
            return Result.Fail("TargetNotFound", "Migration target status not found.");
        }

        // Migrate items
        if (_state.ItemsByProject.TryGetValue(projectId, out var items))
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].StatusId == statusId)
                {
                    items[i] = items[i] with { StatusId = migrateToStatusId, UpdatedAt = DateTime.UtcNow };
                }
            }
        }

        var updatedStatuses = project.Statuses.Where(s => s.Id != statusId).ToList();
        _state.Projects[projIndex] = project with { Statuses = updatedStatuses };

        _state.SchedulePersist();
        _state.NotifyStateChanged();
        _toast.ShowSuccess($"Status {statusToDelete.Name} removed");

        return Result.Ok();
    }

    public Result<Label> AddLabel(Guid projectId, string name, string colorToken)
    {
        var projIndex = _state.Projects.FindIndex(p => p.Id == projectId);
        if (projIndex < 0) return Result<Label>.Fail("NotFound", "Project not found.");

        var project = _state.Projects[projIndex];
        var label = new Label(Guid.NewGuid(), name.Trim(), colorToken);

        var updatedLabels = new List<Label>(project.Labels) { label };
        _state.Projects[projIndex] = project with { Labels = updatedLabels };

        _state.SchedulePersist();
        _state.NotifyStateChanged();

        return Result<Label>.Ok(label);
    }

    public Result DeleteLabel(Guid projectId, Guid labelId)
    {
        var projIndex = _state.Projects.FindIndex(p => p.Id == projectId);
        if (projIndex < 0) return Result.Fail("NotFound", "Project not found.");

        var project = _state.Projects[projIndex];
        var updatedLabels = project.Labels.Where(l => l.Id != labelId).ToList();
        _state.Projects[projIndex] = project with { Labels = updatedLabels };

        // Clean label references in items
        if (_state.ItemsByProject.TryGetValue(projectId, out var items))
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].LabelIds.Contains(labelId))
                {
                    items[i] = items[i] with
                    {
                        LabelIds = items[i].LabelIds.Where(id => id != labelId).ToList(),
                        UpdatedAt = DateTime.UtcNow
                    };
                }
            }
        }

        _state.SchedulePersist();
        _state.NotifyStateChanged();
        return Result.Ok();
    }

    public Result<Milestone> AddMilestone(Guid projectId, string name, DateOnly? targetDate)
    {
        if (!_state.MilestonesByProject.TryGetValue(projectId, out var milestones))
        {
            milestones = new List<Milestone>();
            _state.MilestonesByProject[projectId] = milestones;
        }

        var milestone = new Milestone(Guid.NewGuid(), projectId, name.Trim(), targetDate, MilestoneState.Open);
        milestones.Add(milestone);

        _state.SchedulePersist();
        _state.NotifyStateChanged();
        _toast.ShowSuccess($"Milestone '{milestone.Name}' created");

        return Result<Milestone>.Ok(milestone);
    }

    public Result<Milestone> UpdateMilestone(Guid projectId, Guid milestoneId, string name, DateOnly? targetDate, MilestoneState state)
    {
        if (!_state.MilestonesByProject.TryGetValue(projectId, out var milestones))
        {
            return Result<Milestone>.Fail("NotFound", "Project milestones not found.");
        }

        var idx = milestones.FindIndex(m => m.Id == milestoneId);
        if (idx < 0) return Result<Milestone>.Fail("NotFound", "Milestone not found.");

        var updated = milestones[idx] with { Name = name.Trim(), TargetDate = targetDate, State = state };
        milestones[idx] = updated;

        _state.SchedulePersist();
        _state.NotifyStateChanged();

        return Result<Milestone>.Ok(updated);
    }

    public Result DeleteMilestone(Guid projectId, Guid milestoneId)
    {
        if (_state.MilestonesByProject.TryGetValue(projectId, out var milestones))
        {
            milestones.RemoveAll(m => m.Id == milestoneId);
        }

        // Detach items from this milestone
        if (_state.ItemsByProject.TryGetValue(projectId, out var items))
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].MilestoneId == milestoneId)
                {
                    items[i] = items[i] with { MilestoneId = null, UpdatedAt = DateTime.UtcNow };
                }
            }
        }

        _state.SchedulePersist();
        _state.NotifyStateChanged();
        return Result.Ok();
    }
}
