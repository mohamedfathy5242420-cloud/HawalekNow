namespace HawalekNow.Domain.Modules.Catalog;

// Validates an ancestry stream without a recursive call stack or a business depth cap.
public sealed class CategoryPath
{
    private readonly Guid activityId;
    private readonly HashSet<Guid> visited;
    public CategoryPath(Guid categoryId, Guid activityId)
    {
        if (categoryId == Guid.Empty || activityId == Guid.Empty) throw new ArgumentException("A category and activity are required.");
        this.activityId = activityId; visited = [categoryId];
    }
    public bool TryAppend(Guid ancestorId, Guid ancestorActivityId) => ancestorId != Guid.Empty && ancestorActivityId == activityId && visited.Add(ancestorId);
}
