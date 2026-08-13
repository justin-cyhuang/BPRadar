using Microsoft.Extensions.Caching.Memory;

namespace BPRadar.Web.Features.Import;

public sealed class ImportSessionStore(IMemoryCache cache)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);

    public ImportBatch Add(ImportBatch batch)
    {
        cache.Set(batch.Id, batch, SessionLifetime);
        return batch;
    }

    public bool TryGet(Guid id, out ImportBatch batch) =>
        cache.TryGetValue(id, out batch!);

    public void SetPreview(Guid id, ImportPreview preview)
    {
        if (!cache.TryGetValue(id, out ImportBatch? batch) || batch is null)
        {
            throw new KeyNotFoundException($"Import batch {id} does not exist.");
        }

        cache.Set(id, batch with { Preview = preview }, SessionLifetime);
    }

    public void Remove(Guid id) => cache.Remove(id);
}
