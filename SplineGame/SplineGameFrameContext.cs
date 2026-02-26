using System.Numerics;
using DerpDocDatabase;
using DerpDocDatabase.Prefabs;
using DerpLib.DI;
using DerpLib.Ecs;
using DerpLib.Text;
using FixedMath;
using DerpEngine = DerpLib.Derp;

namespace SplineGame;

public sealed class SplineGameFrameContext : IDisposable
{
    public const int CurveSegmentsPerSplineSegment = 22;
    public const int TriggerCooldownFrames = 20;
    public const float PlayerCollisionRadius = 16f;
    public const float SplineFitPaddingFactor = 0.8f;
    public const string InputHelpText = "A/D or Left/Right: Move   Space: Fire   Q/E: Switch Weapon   F5: Reload   R: Restart";

    private const float TargetDeltaTime = 1f / 60f;

    public static readonly Fixed64 FixedDeltaTime = Fixed64.FromFloat(TargetDeltaTime);
    public static readonly Fixed64 PlayerMoveSpeedParam = Fixed64.FromFloat(0.24f);

    private bool _disposed;
    private bool _reloadRequested;
    private bool _restartRequested;

    public SplineGameFrameContext(
        GameDatabase database,
        SplineLevelBuildService levelBuildService,
        SplinePrefabBakedDataLoader prefabBakedDataLoader,
        SplineSimContext simContext,
        SplineRenderContext renderContext,
        SplineUiAssetPreloadContext uiAssetPreloadContext,
        SplineLevelLifecycleSystem levelLifecycleSystem,
        [Tag("Sim")] EcsSystemPipeline<PrefabSimWorld> simPipeline,
        [Tag("Render")] EcsSystemPipeline<PrefabRenderWorld> renderPipeline)
    {
        Database = database;
        LevelBuildService = levelBuildService;
        PrefabBakedDataLoader = prefabBakedDataLoader;
        SimContext = simContext;
        RenderContext = renderContext;
        UiAssetPreloadContext = uiAssetPreloadContext;
        LevelLifecycleSystem = levelLifecycleSystem;
        SimPipeline = simPipeline;
        RenderPipeline = renderPipeline;

        StatusText = "Loading spline levels...";
        LevelText = "Level: --";
        EntityText = "Enemies: 0 Triggers: 0";
        PrefabSpawnText = "Prefabs: --";

        SimContext.DeltaTime = FixedDeltaTime;

        Database.Reloaded += OnDatabaseReloaded;
        _reloadRequested = true;
    }

    public GameDatabase Database { get; }
    public SplineLevelBuildService LevelBuildService { get; }
    public SplinePrefabBakedDataLoader PrefabBakedDataLoader { get; }
    public SplineSimContext SimContext { get; }
    public SplineRenderContext RenderContext { get; }
    public SplineUiAssetPreloadContext UiAssetPreloadContext { get; }
    public SplineLevelLifecycleSystem LevelLifecycleSystem { get; }
    public EcsSystemPipeline<PrefabSimWorld> SimPipeline { get; }
    public EcsSystemPipeline<PrefabRenderWorld> RenderPipeline { get; }

    public bool IsRuntimeInitialized { get; private set; }
    public Font UiFont { get; private set; } = null!;
    public SplineUiAssetCache? UiAssetCache { get; private set; }

    public string StatusText { get; set; }
    public string LevelText { get; set; }
    public string EntityText { get; set; }
    public string PrefabSpawnText { get; set; }
    public bool HasPrefabLoadError { get; set; }

    public Fixed64 ReferenceSplineLengthFixed { get; set; }
    public float GlobalBoundsMinX { get; set; }
    public float GlobalBoundsMinY { get; set; }
    public float GlobalBoundsMaxX { get; set; }
    public float GlobalBoundsMaxY { get; set; }

    public Vector2[] SplinePolylinePoints { get; private set; } = Array.Empty<Vector2>();

    public void Initialize(Font uiFont)
    {
        if (IsRuntimeInitialized)
        {
            return;
        }

        UiFont = uiFont;
        DerpEngine.SetSdfFontAtlas(UiFont.Atlas);

        UiAssetCache = new SplineUiAssetCache(DerpEngine.Engine.Content, UiFont);
        RenderContext.UiAssetCache = UiAssetCache;
        UiAssetPreloadContext.UiAssetCache = UiAssetCache;
        IsRuntimeInitialized = true;
    }

    public void Shutdown()
    {
        if (!IsRuntimeInitialized)
        {
            return;
        }

        UiAssetCache?.Dispose();
        UiAssetCache = null;
        RenderContext.UiAssetCache = null;
        UiAssetPreloadContext.UiAssetCache = null;
        _restartRequested = false;

        IsRuntimeInitialized = false;
    }

    public bool TryConsumeReloadRequest()
    {
        if (!_reloadRequested)
        {
            return false;
        }

        _reloadRequested = false;
        return true;
    }

    public void RequestReload()
    {
        _reloadRequested = true;
    }

    public bool TryConsumeRestartRequest()
    {
        if (!_restartRequested)
        {
            return false;
        }

        _restartRequested = false;
        return true;
    }

    public void RequestRestart()
    {
        _restartRequested = true;
    }

    public void EnsureSplinePolylineCapacity(int requiredCapacity)
    {
        if (SplinePolylinePoints.Length >= requiredCapacity)
        {
            return;
        }

        SplinePolylinePoints = new Vector2[requiredCapacity];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Database.Reloaded -= OnDatabaseReloaded;
        Shutdown();
        Database.Dispose();
    }

    private void OnDatabaseReloaded()
    {
        RequestReload();
    }
}
