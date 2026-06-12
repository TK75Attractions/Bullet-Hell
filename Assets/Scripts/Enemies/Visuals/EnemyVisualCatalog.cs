using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class EnemyVisualCatalog
{
    private readonly Dictionary<string, EnemyVisualSetRuntime> visualsById = new Dictionary<string, EnemyVisualSetRuntime>();
    private readonly List<AsyncOperationHandle<EnemyVisualSetAsset>> addressableHandles = new List<AsyncOperationHandle<EnemyVisualSetAsset>>();

    public static EnemyVisualCatalog Empty { get; } = new EnemyVisualCatalog();

    public void AddVisual(EnemyVisualSetRuntime visual)
    {
        if (visual == null || string.IsNullOrWhiteSpace(visual.id))
        {
            return;
        }

        visualsById[visual.id] = visual;
    }

    public void AddAddressableHandle(AsyncOperationHandle<EnemyVisualSetAsset> handle)
    {
        if (handle.IsValid())
        {
            addressableHandles.Add(handle);
        }
    }

    public EnemyVisualSetRuntime GetVisual(string visualId)
    {
        if (string.IsNullOrWhiteSpace(visualId))
        {
            return null;
        }

        visualsById.TryGetValue(visualId, out EnemyVisualSetRuntime visual);
        return visual;
    }

    public void Release()
    {
        foreach (EnemyVisualSetRuntime visual in visualsById.Values)
        {
            visual?.ReleaseOwnedObjects();
        }

        visualsById.Clear();

        for (int i = 0; i < addressableHandles.Count; i++)
        {
            AsyncOperationHandle<EnemyVisualSetAsset> handle = addressableHandles[i];
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        addressableHandles.Clear();
    }
}
