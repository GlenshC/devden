using Microsoft.JSInterop;

namespace DevDen.Services;

public class HotkeyService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly AppState _state;
    private readonly CommandDispatcher _dispatcher;
    private DotNetObjectReference<HotkeyService>? _dotNetRef;
    private bool _isRegistered;

    public HotkeyService(IJSRuntime js, AppState state, CommandDispatcher dispatcher)
    {
        _js = js;
        _state = state;
        _dispatcher = dispatcher;
    }

    public async Task RegisterAsync()
    {
        if (_isRegistered) return;
        _dotNetRef = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("denHotkeys.register", _dotNetRef);
        _isRegistered = true;
    }

    [JSInvokable]
    public void OnCommandBarHotkey()
    {
        _state.ToggleCommandBar();
    }

    [JSInvokable]
    public void OnEscapeHotkey()
    {
        if (_state.IsCommandBarOpen)
        {
            _state.ToggleCommandBar(false);
        }
        else if (_state.IsDetailOpen)
        {
            _state.CloseDetail();
        }
    }

    [JSInvokable]
    public void OnNewItemHotkey()
    {
        if (!_state.IsCommandBarOpen && !_state.IsDetailOpen)
        {
            _dispatcher.TriggerNewItem();
        }
    }

    [JSInvokable]
    public void OnFilterHotkey()
    {
        if (!_state.IsCommandBarOpen && !_state.IsDetailOpen)
        {
            _dispatcher.TriggerFocusFilter();
        }
    }

    [JSInvokable]
    public void OnNextItemHotkey()
    {
        // Select next item in list / board if items exist
        var items = _state.ActiveItems;
        if (items.Count == 0) return;

        if (_state.SelectedItem == null)
        {
            _state.SetSelectedItem(items[0]);
        }
        else
        {
            var idx = items.ToList().FindIndex(i => i.Id == _state.SelectedItem.Id);
            if (idx >= 0 && idx < items.Count - 1)
            {
                _state.SetSelectedItem(items[idx + 1]);
            }
        }
    }

    [JSInvokable]
    public void OnPrevItemHotkey()
    {
        var items = _state.ActiveItems;
        if (items.Count == 0) return;

        if (_state.SelectedItem == null)
        {
            _state.SetSelectedItem(items[^1]);
        }
        else
        {
            var idx = items.ToList().FindIndex(i => i.Id == _state.SelectedItem.Id);
            if (idx > 0)
            {
                _state.SetSelectedItem(items[idx - 1]);
            }
        }
    }

    [JSInvokable]
    public void OnOpenItemHotkey()
    {
        // Enter pressed
    }

    [JSInvokable]
    public void OnSubmitFormHotkey()
    {
        // Cmd/Ctrl + Enter
    }

    [JSInvokable]
    public void OnPrevViewHotkey()
    {
        // '[' pressed - cycle views
        var views = new[] { "board", "list", "roadmap" };
        var key = _state.ActiveProject?.Key ?? "DEN";
        var current = _state.CurrentRoute.ToLowerInvariant();

        if (current.Contains("roadmap")) _dispatcher.NavigateTo($"/p/{key}/list");
        else if (current.Contains("list")) _dispatcher.NavigateTo($"/p/{key}/board");
        else if (current.Contains("board")) _dispatcher.NavigateTo($"/p/{key}/roadmap");
    }

    [JSInvokable]
    public void OnNextViewHotkey()
    {
        // ']' pressed - cycle views
        var key = _state.ActiveProject?.Key ?? "DEN";
        var current = _state.CurrentRoute.ToLowerInvariant();

        if (current.Contains("board")) _dispatcher.NavigateTo($"/p/{key}/list");
        else if (current.Contains("list")) _dispatcher.NavigateTo($"/p/{key}/roadmap");
        else if (current.Contains("roadmap")) _dispatcher.NavigateTo($"/p/{key}/board");
    }

    [JSInvokable]
    public void OnToggleTodayHotkey()
    {
        _dispatcher.ToggleTodayOnSelected();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isRegistered)
        {
            try
            {
                await _js.InvokeVoidAsync("denHotkeys.unregister");
            }
            catch
            {
                // ignore on shutdown
            }
            _dotNetRef?.Dispose();
            _isRegistered = false;
        }
    }
}
