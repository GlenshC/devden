namespace DevDen.Services;

public class DebouncedPersister : IDisposable
{
    private readonly IDataStore _store;
    private readonly Func<AppState> _appStateAccessor;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();
    private bool _isDirty = false;

    public DebouncedPersister(IDataStore store, Func<AppState> appStateAccessor)
    {
        _store = store;
        _appStateAccessor = appStateAccessor;
    }

    public void SchedulePersist()
    {
        lock (_lock)
        {
            _isDirty = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(250, token);
                    if (!token.IsCancellationRequested)
                    {
                        await FlushAsync();
                    }
                }
                catch (TaskCanceledException)
                {
                    // expected during rapid debounce
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Debounced persister error: {ex.Message}");
                }
            }, token);
        }
    }

    public async Task FlushAsync()
    {
        AppState state;
        lock (_lock)
        {
            if (!_isDirty) return;
            _isDirty = false;
            state = _appStateAccessor();
        }

        try
        {
            var index = new Models.StorageIndex(
                Version: Models.StorageIndex.CurrentVersion,
                ProjectIds: state.Projects.Select(p => p.Id).ToList(),
                Prefs: state.Prefs
            );

            await _store.SaveIndexAsync(index);

            foreach (var p in state.Projects)
            {
                await _store.SaveProjectAsync(p);

                if (state.ItemsByProject.TryGetValue(p.Id, out var items))
                {
                    await _store.SaveItemsAsync(p.Id, items);
                }

                if (state.MilestonesByProject.TryGetValue(p.Id, out var milestones))
                {
                    await _store.SaveMilestonesAsync(p.Id, milestones);
                }

                if (state.ActivityByProject.TryGetValue(p.Id, out var activity))
                {
                    await _store.SaveActivityAsync(p.Id, activity);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FlushAsync failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
