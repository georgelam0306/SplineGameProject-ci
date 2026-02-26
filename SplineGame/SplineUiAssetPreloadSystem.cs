using DerpDocDatabase;
using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;

namespace SplineGame;

public sealed class SplineUiAssetPreloadSystem : IEcsSystem<PrefabRenderWorld>
{
    private readonly SplineUiAssetPreloadContext _context;

    public SplineUiAssetPreloadSystem(SplineUiAssetPreloadContext context)
    {
        _context = context;
    }

    public void Update(PrefabRenderWorld world)
    {
        if (!_context.PreloadRequested)
        {
            return;
        }

        _context.CompletePreload();

        SplineUiAssetCache? uiAssetCache = _context.UiAssetCache;
        if (uiAssetCache == null)
        {
            return;
        }

        uiAssetCache.Clear();

        var renderShape = world.SimEntityLink_SplineEntityVisual;
        for (int renderRow = 0; renderRow < renderShape.Count; renderRow++)
        {
            ref readonly SplineEntityVisualComponent visual = ref renderShape.SplineEntityVisual(renderRow);
            if (!_context.Database.Ui.TryFindById(visual.UiAsset, out Ui uiRow))
            {
                continue;
            }

            uiAssetCache.Preload(uiRow.RelativePath);
        }
    }
}
