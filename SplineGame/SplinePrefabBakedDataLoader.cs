using System.IO;
using DerpDocDatabase.Prefabs;

namespace SplineGame;

public sealed class SplinePrefabBakedDataLoader
{
    private readonly string _databaseDirectoryPath;

    public SplinePrefabBakedDataLoader()
    {
        string baseDirectoryPath = AppContext.BaseDirectory;
        _databaseDirectoryPath = Path.Combine(baseDirectoryPath, "Resources", "Database");
    }

    public bool TryLoad(
        out PrefabSimWorldUgcBakedAssets.BakedData simBaked,
        out PrefabRenderWorldUgcBakedAssets.BakedData renderBaked,
        out string errorText)
    {
        simBaked = default;
        renderBaked = default;
        errorText = "";

        string simPath = Path.Combine(_databaseDirectoryPath, "PrefabSimWorld.derpentitydata");
        string renderPath = Path.Combine(_databaseDirectoryPath, "PrefabRenderWorld.derpentitydata");

        if (!File.Exists(simPath) || !File.Exists(renderPath))
        {
            errorText = "missing .derpentitydata";
            return false;
        }

        try
        {
            byte[] simBytes = File.ReadAllBytes(simPath);
            byte[] renderBytes = File.ReadAllBytes(renderPath);
            simBaked = PrefabSimWorldUgcBakedAssets.LoadBakedData(simBytes);
            renderBaked = PrefabRenderWorldUgcBakedAssets.LoadBakedData(renderBytes);
            return true;
        }
        catch (Exception exception)
        {
            errorText = "load failed: " + exception.GetType().Name;
            return false;
        }
    }
}
