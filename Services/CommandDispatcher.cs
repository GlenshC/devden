using DevDen.Models;
using Microsoft.AspNetCore.Components;

namespace DevDen.Services;

public class CommandDispatcher
{
    private readonly AppState _state;
    private readonly ProjectService _projectService;
    private readonly ItemService _itemService;
    private readonly ToastService _toast;
    private readonly NavigationManager _nav;

    public event Action? OnOpenNewItemModal;
    public event Action? OnFocusFilterInput;

    public CommandDispatcher(
        AppState state,
        ProjectService projectService,
        ItemService itemService,
        ToastService toast,
        NavigationManager nav)
    {
        _state = state;
        _projectService = projectService;
        _itemService = itemService;
        _toast = toast;
        _nav = nav;
    }

    public void TriggerNewItem()
    {
        OnOpenNewItemModal?.Invoke();
    }

    public void TriggerFocusFilter()
    {
        OnFocusFilterInput?.Invoke();
    }

    public void NavigateTo(string relativePath)
    {
        _nav.NavigateTo(relativePath);
    }

    public void GoToBoard()
    {
        var key = _state.ActiveProject?.Key ?? "DEN";
        _nav.NavigateTo($"/p/{key}/board");
    }

    public void GoToList()
    {
        var key = _state.ActiveProject?.Key ?? "DEN";
        _nav.NavigateTo($"/p/{key}/list");
    }

    public void GoToRoadmap()
    {
        var key = _state.ActiveProject?.Key ?? "DEN";
        _nav.NavigateTo($"/p/{key}/roadmap");
    }

    public void GoToToday()
    {
        _nav.NavigateTo("/today");
    }

    public void GoToSettings()
    {
        _nav.NavigateTo("/settings");
    }

    public void SwitchProject(Guid projectId)
    {
        var p = _state.Projects.FirstOrDefault(x => x.Id == projectId);
        if (p != null)
        {
            _state.SetActiveProject(p.Id);
            var currentUri = _nav.Uri;
            if (currentUri.Contains("/list"))
            {
                _nav.NavigateTo($"/p/{p.Key}/list");
            }
            else if (currentUri.Contains("/roadmap"))
            {
                _nav.NavigateTo($"/p/{p.Key}/roadmap");
            }
            else
            {
                _nav.NavigateTo($"/p/{p.Key}/board");
            }
        }
    }

    public void ToggleTodayOnSelected()
    {
        if (_state.SelectedItem != null)
        {
            _itemService.ToggleToday(_state.SelectedItem.Id);
        }
    }

    public void SetStatusOnSelected(Guid statusId)
    {
        if (_state.SelectedItem != null)
        {
            _itemService.MoveItem(_state.SelectedItem.Id, statusId);
        }
    }

    public void SetPriorityOnSelected(Priority priority)
    {
        if (_state.SelectedItem != null)
        {
            _itemService.UpdateItem(_state.SelectedItem.Id, new ItemPatch(Priority: priority));
        }
    }
}
