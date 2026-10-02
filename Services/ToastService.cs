namespace DevDen.Services;

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error
}

public record ToastMessage(
    Guid Id,
    string Message,
    ToastType Type,
    int DurationMs,
    string? ActionLabel,
    Action? OnAction
);

public class ToastService
{
    public event Action<ToastMessage>? OnShowToast;

    public void Show(string message, ToastType type = ToastType.Info, int durationMs = 4000, string? actionLabel = null, Action? onAction = null)
    {
        var toast = new ToastMessage(Guid.NewGuid(), message, type, durationMs, actionLabel, onAction);
        OnShowToast?.Invoke(toast);
    }

    public void ShowSuccess(string message) => Show(message, ToastType.Success, 3000);
    public void ShowError(string message, string? actionLabel = null, Action? onAction = null) =>
        Show(message, ToastType.Error, 5000, actionLabel, onAction);
    public void ShowWarning(string message) => Show(message, ToastType.Warning, 4000);
    public void ShowInfo(string message) => Show(message, ToastType.Info, 3000);
}
