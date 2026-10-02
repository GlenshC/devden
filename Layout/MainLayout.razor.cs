using DevDen.Models;
using DevDen.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DevDen.Layout;

public partial class MainLayout : IAsyncDisposable
{
    private bool _isQuickCreateOpen;
    private string _quickTitle = "";
    private Guid _quickStatusId;
    private ItemType _quickType = ItemType.Task;
    private Priority _quickPriority = Priority.P2;
    private string? _quickMilestoneId;
    private bool _quickInToday;

    [Microsoft.AspNetCore.Components.Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Hotkeys.RegisterAsync();
            Dispatcher.OnOpenNewItemModal += HandleOpenNewItemModal;
        }
    }

    private void HandleOpenNewItemModal()
    {
        if (AppState.ActiveProject == null) return;

        var defaultStatus = AppState.ActiveStatuses.FirstOrDefault(s => s.Category == StatusCategory.Active)
            ?? AppState.ActiveStatuses.FirstOrDefault();

        _quickStatusId = defaultStatus?.Id ?? Guid.Empty;
        _quickTitle = "";
        _quickType = ItemType.Task;
        _quickPriority = Priority.P2;
        _quickMilestoneId = null;
        _quickInToday = false;
        _isQuickCreateOpen = true;

        StateHasChanged();
        _ = JSRuntime.InvokeVoidAsync("denUtils.focusElement", "quick-create-title");
    }

    private void CloseQuickCreate()
    {
        _isQuickCreateOpen = false;
        StateHasChanged();
    }

    private void HandleTitleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            SubmitQuickCreate();
        }
    }

    private void SubmitQuickCreate()
    {
        if (string.IsNullOrWhiteSpace(_quickTitle) || AppState.ActiveProjectId == null) return;

        Guid? milestoneGuid = null;
        if (!string.IsNullOrEmpty(_quickMilestoneId) && Guid.TryParse(_quickMilestoneId, out var parsedMg))
        {
            milestoneGuid = parsedMg;
        }

        var draft = new ItemDraft(
            Title: _quickTitle,
            Body: null,
            StatusId: _quickStatusId,
            Type: _quickType,
            Priority: _quickPriority,
            MilestoneId: milestoneGuid,
            InToday: _quickInToday
        );

        var result = ItemService.CreateItem(AppState.ActiveProjectId.Value, draft);
        if (result.IsSuccess)
        {
            _isQuickCreateOpen = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dispatcher.OnOpenNewItemModal -= HandleOpenNewItemModal;
        await Hotkeys.DisposeAsync();
    }
}
