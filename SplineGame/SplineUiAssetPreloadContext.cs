using DerpDocDatabase;

namespace SplineGame;

public sealed class SplineUiAssetPreloadContext
{
    public SplineUiAssetPreloadContext(GameDatabase database)
    {
        Database = database;
    }

    public GameDatabase Database { get; }
    public SplineUiAssetCache? UiAssetCache { get; set; }
    public bool PreloadRequested { get; private set; }

    public void RequestPreload()
    {
        PreloadRequested = true;
    }

    public void CompletePreload()
    {
        PreloadRequested = false;
    }
}
