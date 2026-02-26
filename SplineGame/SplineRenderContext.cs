using System.Numerics;
using DerpDocDatabase;
using DerpDocDatabase.Prefabs;

namespace SplineGame;

public sealed class SplineRenderContext
{
    public SplineRenderContext(GameDatabase database)
    {
        Database = database;
    }

    public GameDatabase Database { get; }
    public PrefabSimWorld? SimWorld { get; private set; }
    public SplineCompiledLevel.Point[] LevelPoints { get; private set; } = Array.Empty<SplineCompiledLevel.Point>();
    public Vector2 SplineCentroid { get; private set; }
    public float WorldScale { get; private set; }
    public float WorldOffsetX { get; private set; }
    public float WorldOffsetY { get; private set; }
    public SplineUiAssetCache? UiAssetCache { get; set; }

    public void SetFrame(
        PrefabSimWorld simWorld,
        SplineCompiledLevel.Point[] levelPoints,
        Vector2 splineCentroid,
        float worldScale,
        float worldOffsetX,
        float worldOffsetY)
    {
        SimWorld = simWorld;
        LevelPoints = levelPoints;
        SplineCentroid = splineCentroid;
        WorldScale = worldScale;
        WorldOffsetX = worldOffsetX;
        WorldOffsetY = worldOffsetY;
    }

    public void ResetFrame()
    {
        SimWorld = null;
        LevelPoints = Array.Empty<SplineCompiledLevel.Point>();
        SplineCentroid = Vector2.Zero;
        WorldScale = 1f;
        WorldOffsetX = 0f;
        WorldOffsetY = 0f;
    }
}
