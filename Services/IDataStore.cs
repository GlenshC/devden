using DevDen.Models;

namespace DevDen.Services;

public interface IDataStore
{
    Task<StoreSnapshot> LoadAllAsync();
    Task SaveIndexAsync(StorageIndex index);
    Task SaveProjectAsync(Project p);
    Task SaveItemsAsync(Guid projectId, IReadOnlyList<Item> items);
    Task SaveMilestonesAsync(Guid projectId, IReadOnlyList<Milestone> milestones);
    Task SaveActivityAsync(Guid projectId, IReadOnlyList<ActivityEntry> log);
    Task DeleteProjectAsync(Guid projectId);
    Task<string> ExportAsync();
    Task ImportAsync(string json, ImportMode mode);
}
