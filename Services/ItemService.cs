using DevDen.Models;
using DevDen.Models.Common;

namespace DevDen.Services;

public record ItemDraft(
    string Title,
    string? Body,
    Guid StatusId,
    ItemType Type,
    Priority Priority,
    List<Guid>? LabelIds = null,
    Guid? MilestoneId = null,
    DateOnly? DueDate = null,
    bool InToday = false
);

public record ItemPatch(
    string? Title = null,
    string? Body = null,
    Guid? StatusId = null,
    ItemType? Type = null,
    Priority? Priority = null,
    List<Guid>? LabelIds = null,
    Guid? MilestoneId = null,
    DateOnly? DueDate = null,
    bool? InToday = null
);

public class ItemService
{
    private readonly AppState _state;
    private readonly ToastService _toast;

    public ItemService(AppState state, ToastService toast)
    {
        _state = state;
        _toast = toast;
    }

    public Result<Item> CreateItem(Guid projectId, ItemDraft draft)
    {
        var title = draft.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            return Result<Item>.Fail("TitleInvalid", "Title must be between 1 and 200 characters.");
        }

        var projIndex = _state.Projects.FindIndex(p => p.Id == projectId);
        if (projIndex < 0)
        {
            return Result<Item>.Fail("ProjectNotFound", "Project not found.");
        }

        var project = _state.Projects[projIndex];
        var itemNumber = project.NextItemNumber;

        // Increment project monotonic item counter
        _state.Projects[projIndex] = project with { NextItemNumber = itemNumber + 1 };

        if (!_state.ItemsByProject.TryGetValue(projectId, out var items))
        {
            items = new List<Item>();
            _state.ItemsByProject[projectId] = items;
        }

        var columnItems = items.Where(i => i.StatusId == draft.StatusId).ToList();
        double order = columnItems.Count > 0 ? columnItems.Max(i => i.Order) + FractionalOrder.Step : FractionalOrder.Step;

        var status = project.Statuses.FirstOrDefault(s => s.Id == draft.StatusId);
        var isDone = status?.Category == StatusCategory.Done;

        var newItem = new Item(
            Id: Guid.NewGuid(),
            ProjectId: projectId,
            Number: itemNumber,
            Type: draft.Type,
            Title: title,
            Body: draft.Body,
            StatusId: draft.StatusId,
            Order: order,
            Priority: draft.Priority,
            LabelIds: draft.LabelIds ?? new(),
            MilestoneId: draft.MilestoneId,
            Checklist: new(),
            DueDate: draft.DueDate,
            InToday: draft.InToday,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            CompletedAt: isDone ? DateTime.UtcNow : null
        );

        items.Add(newItem);

        // Record Activity
        LogActivity(projectId, newItem.Id, ActivityKind.Created, null, $"{project.Key}-{itemNumber} created");

        _state.SchedulePersist();
        _state.NotifyStateChanged();
        _toast.ShowSuccess($"Created {project.Key}-{itemNumber}: {title}");

        return Result<Item>.Ok(newItem);
    }

    public Result<Item> MoveItem(Guid itemId, Guid targetStatusId, Guid? beforeItemId = null, Guid? afterItemId = null)
    {
        var (item, projectId, index) = FindItem(itemId);
        if (item == null) return Result<Item>.Fail("NotFound", "Item not found.");

        var project = _state.Projects.FirstOrDefault(p => p.Id == projectId);
        if (project == null) return Result<Item>.Fail("ProjectNotFound", "Project not found.");

        var targetStatus = project.Statuses.FirstOrDefault(s => s.Id == targetStatusId);
        if (targetStatus == null) return Result<Item>.Fail("StatusNotFound", "Target status not found.");

        var oldStatus = project.Statuses.FirstOrDefault(s => s.Id == item.StatusId);
        var items = _state.ItemsByProject[projectId];

        // Compute new order
        var columnItems = items.Where(i => i.StatusId == targetStatusId && i.Id != itemId).OrderBy(i => i.Order).ToList();
        double newOrder;

        if (beforeItemId.HasValue)
        {
            var beforeItem = columnItems.FirstOrDefault(i => i.Id == beforeItemId.Value);
            if (beforeItem != null)
            {
                var prevItem = columnItems.TakeWhile(i => i.Id != beforeItemId.Value).LastOrDefault();
                if (prevItem != null)
                {
                    if (FractionalOrder.NeedsRebalance(prevItem.Order, beforeItem.Order))
                    {
                        var rebalanced = FractionalOrder.RebalanceColumn(columnItems);
                        for (int r = 0; r < rebalanced.Count; r++)
                        {
                            var rItem = rebalanced[r];
                            var rIdx = items.FindIndex(it => it.Id == rItem.Id);
                            if (rIdx >= 0) items[rIdx] = rItem;
                        }
                        var rbBefore = rebalanced.First(i => i.Id == beforeItemId.Value);
                        var rbPrev = rebalanced.TakeWhile(i => i.Id != beforeItemId.Value).Last();
                        newOrder = FractionalOrder.CalculateBetween(rbPrev.Order, rbBefore.Order);
                    }
                    else
                    {
                        newOrder = FractionalOrder.CalculateBetween(prevItem.Order, beforeItem.Order);
                    }
                }
                else
                {
                    newOrder = FractionalOrder.CalculateBefore(beforeItem.Order);
                }
            }
            else
            {
                newOrder = columnItems.Count > 0 ? columnItems.Last().Order + FractionalOrder.Step : FractionalOrder.Step;
            }
        }
        else if (afterItemId.HasValue)
        {
            var afterItem = columnItems.FirstOrDefault(i => i.Id == afterItemId.Value);
            if (afterItem != null)
            {
                var nextItem = columnItems.SkipWhile(i => i.Id != afterItemId.Value).Skip(1).FirstOrDefault();
                if (nextItem != null)
                {
                    if (FractionalOrder.NeedsRebalance(afterItem.Order, nextItem.Order))
                    {
                        var rebalanced = FractionalOrder.RebalanceColumn(columnItems);
                        for (int r = 0; r < rebalanced.Count; r++)
                        {
                            var rItem = rebalanced[r];
                            var rIdx = items.FindIndex(it => it.Id == rItem.Id);
                            if (rIdx >= 0) items[rIdx] = rItem;
                        }
                        var rbAfter = rebalanced.First(i => i.Id == afterItemId.Value);
                        var rbNext = rebalanced.SkipWhile(i => i.Id != afterItemId.Value).Skip(1).First();
                        newOrder = FractionalOrder.CalculateBetween(rbAfter.Order, rbNext.Order);
                    }
                    else
                    {
                        newOrder = FractionalOrder.CalculateBetween(afterItem.Order, nextItem.Order);
                    }
                }
                else
                {
                    newOrder = FractionalOrder.CalculateAfter(afterItem.Order);
                }
            }
            else
            {
                newOrder = columnItems.Count > 0 ? columnItems.Last().Order + FractionalOrder.Step : FractionalOrder.Step;
            }
        }
        else
        {
            newOrder = columnItems.Count > 0 ? columnItems.Last().Order + FractionalOrder.Step : FractionalOrder.Step;
        }

        // Done category transitions
        DateTime? completedAt = item.CompletedAt;
        if (targetStatus.Category == StatusCategory.Done && oldStatus?.Category != StatusCategory.Done)
        {
            completedAt = DateTime.UtcNow;
            LogActivity(projectId, item.Id, ActivityKind.Completed, oldStatus?.Name, "Completed");
        }
        else if (targetStatus.Category != StatusCategory.Done && oldStatus?.Category == StatusCategory.Done)
        {
            completedAt = null;
        }

        if (item.StatusId != targetStatusId)
        {
            LogActivity(projectId, item.Id, ActivityKind.StatusChanged, oldStatus?.Name, targetStatus.Name);
        }

        var updated = item with
        {
            StatusId = targetStatusId,
            Order = newOrder,
            CompletedAt = completedAt,
            UpdatedAt = DateTime.UtcNow
        };

        items[index] = updated;

        if (_state.SelectedItem?.Id == itemId)
        {
            _state.SelectedItem = updated;
        }

        _state.SchedulePersist();
        _state.NotifyStateChanged();

        return Result<Item>.Ok(updated);
    }

    public Result<Item> UpdateItem(Guid itemId, ItemPatch patch)
    {
        var (item, projectId, index) = FindItem(itemId);
        if (item == null) return Result<Item>.Fail("NotFound", "Item not found.");

        var project = _state.Projects.First(p => p.Id == projectId);
        var oldStatus = project.Statuses.FirstOrDefault(s => s.Id == item.StatusId);

        var title = patch.Title != null ? patch.Title.Trim() : item.Title;
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            return Result<Item>.Fail("TitleInvalid", "Title must be between 1 and 200 characters.");
        }

        var statusId = patch.StatusId ?? item.StatusId;
        var priority = patch.Priority ?? item.Priority;
        var newStatus = project.Statuses.FirstOrDefault(s => s.Id == statusId);

        DateTime? completedAt = item.CompletedAt;
        if (newStatus?.Category == StatusCategory.Done && oldStatus?.Category != StatusCategory.Done)
        {
            completedAt = DateTime.UtcNow;
            LogActivity(projectId, item.Id, ActivityKind.Completed, oldStatus?.Name, "Completed");
        }
        else if (newStatus?.Category != StatusCategory.Done && oldStatus?.Category == StatusCategory.Done)
        {
            completedAt = null;
        }

        if (statusId != item.StatusId)
        {
            LogActivity(projectId, item.Id, ActivityKind.StatusChanged, oldStatus?.Name, newStatus?.Name);
        }

        if (priority != item.Priority)
        {
            LogActivity(projectId, item.Id, ActivityKind.PriorityChanged, item.Priority.ToString(), priority.ToString());
        }

        var updated = item with
        {
            Title = title,
            Body = patch.Body ?? item.Body,
            StatusId = statusId,
            Type = patch.Type ?? item.Type,
            Priority = priority,
            LabelIds = patch.LabelIds ?? item.LabelIds,
            MilestoneId = patch.MilestoneId ?? item.MilestoneId,
            DueDate = patch.DueDate ?? item.DueDate,
            InToday = patch.InToday ?? item.InToday,
            CompletedAt = completedAt,
            UpdatedAt = DateTime.UtcNow
        };

        _state.ItemsByProject[projectId][index] = updated;

        if (_state.SelectedItem?.Id == itemId)
        {
            _state.SelectedItem = updated;
        }

        _state.SchedulePersist();
        _state.NotifyStateChanged();

        return Result<Item>.Ok(updated);
    }

    public Result DeleteItem(Guid itemId)
    {
        var (item, projectId, index) = FindItem(itemId);
        if (item == null) return Result.Fail("NotFound", "Item not found.");

        var project = _state.Projects.First(p => p.Id == projectId);
        var items = _state.ItemsByProject[projectId];
        items.RemoveAt(index);

        if (_state.SelectedItem?.Id == itemId)
        {
            _state.CloseDetail();
        }

        _state.SchedulePersist();
        _state.NotifyStateChanged();

        _toast.Show(
            $"Deleted {project.Key}-{item.Number}",
            ToastType.Info,
            5000,
            "Undo",
            () =>
            {
                // Undo action
                if (_state.ItemsByProject.TryGetValue(projectId, out var currentList))
                {
                    currentList.Add(item);
                    _state.SchedulePersist();
                    _state.NotifyStateChanged();
                }
            }
        );

        return Result.Ok();
    }

    public Result<bool> ToggleToday(Guid itemId)
    {
        var (item, projectId, index) = FindItem(itemId);
        if (item == null) return Result<bool>.Fail("NotFound", "Item not found.");

        var newInToday = !item.InToday;
        var updated = item with { InToday = newInToday, UpdatedAt = DateTime.UtcNow };
        _state.ItemsByProject[projectId][index] = updated;

        if (_state.SelectedItem?.Id == itemId)
        {
            _state.SelectedItem = updated;
        }

        _state.SchedulePersist();
        _state.NotifyStateChanged();

        var message = newInToday ? "Added to Today" : "Removed from Today";
        _toast.ShowInfo(message);

        return Result<bool>.Ok(newInToday);
    }

    public Result<ChecklistEntry> AddChecklistEntry(Guid itemId, string text)
    {
        var (item, projectId, index) = FindItem(itemId);
        if (item == null) return Result<ChecklistEntry>.Fail("NotFound", "Item not found.");

        var trimmed = text?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return Result<ChecklistEntry>.Fail("EmptyText", "Checklist text cannot be empty.");

        double maxOrder = item.Checklist.Count > 0 ? item.Checklist.Max(c => c.Order) : 0.0;
        var entry = new ChecklistEntry(Guid.NewGuid(), trimmed, false, maxOrder + 100);

        var updatedChecklist = new List<ChecklistEntry>(item.Checklist) { entry };
        var updated = item with { Checklist = updatedChecklist, UpdatedAt = DateTime.UtcNow };
        _state.ItemsByProject[projectId][index] = updated;

        if (_state.SelectedItem?.Id == itemId) _state.SelectedItem = updated;

        _state.SchedulePersist();
        _state.NotifyStateChanged();

        return Result<ChecklistEntry>.Ok(entry);
    }

    public Result ToggleChecklistEntry(Guid itemId, Guid entryId)
    {
        var (item, projectId, index) = FindItem(itemId);
        if (item == null) return Result.Fail("NotFound", "Item not found.");

        var entryIndex = item.Checklist.FindIndex(c => c.Id == entryId);
        if (entryIndex < 0) return Result.Fail("NotFound", "Checklist item not found.");

        var entry = item.Checklist[entryIndex];
        var updatedEntry = entry with { Done = !entry.Done };

        var updatedChecklist = new List<ChecklistEntry>(item.Checklist);
        updatedChecklist[entryIndex] = updatedEntry;

        var updated = item with { Checklist = updatedChecklist, UpdatedAt = DateTime.UtcNow };
        _state.ItemsByProject[projectId][index] = updated;

        if (_state.SelectedItem?.Id == itemId) _state.SelectedItem = updated;

        _state.SchedulePersist();
        _state.NotifyStateChanged();

        return Result.Ok();
    }

    public Result DeleteChecklistEntry(Guid itemId, Guid entryId)
    {
        var (item, projectId, index) = FindItem(itemId);
        if (item == null) return Result.Fail("NotFound", "Item not found.");

        var updatedChecklist = item.Checklist.Where(c => c.Id != entryId).ToList();
        var updated = item with { Checklist = updatedChecklist, UpdatedAt = DateTime.UtcNow };
        _state.ItemsByProject[projectId][index] = updated;

        if (_state.SelectedItem?.Id == itemId) _state.SelectedItem = updated;

        _state.SchedulePersist();
        _state.NotifyStateChanged();

        return Result.Ok();
    }

    private (Item? item, Guid projectId, int index) FindItem(Guid itemId)
    {
        foreach (var (pid, list) in _state.ItemsByProject)
        {
            var idx = list.FindIndex(i => i.Id == itemId);
            if (idx >= 0) return (list[idx], pid, idx);
        }
        return (null, Guid.Empty, -1);
    }

    private void LogActivity(Guid projectId, Guid itemId, ActivityKind kind, string? from, string? to)
    {
        if (!_state.ActivityByProject.TryGetValue(projectId, out var actList))
        {
            actList = new List<ActivityEntry>();
            _state.ActivityByProject[projectId] = actList;
        }

        var entry = new ActivityEntry(Guid.NewGuid(), itemId, kind, from, to, DateTime.UtcNow);
        actList.Add(entry);
    }
}
