using DerpDocDatabase.Prefabs;
using DerpLib.DI;
using DerpLib.Ecs;
using FixedMath;

namespace SplineGame;

public sealed class SplineLevelLifecycleSystem
{
    private readonly SplineSimContext _simContext;
    private readonly SplineLevelSpawnContext _spawnContext;
    private readonly EcsSystemPipeline<PrefabSimWorld> _spawnPipeline;
    private readonly SplineUiAssetPreloadContext _uiAssetPreloadContext;
    private readonly EcsSystemPipeline<PrefabRenderWorld> _uiAssetPreloadPipeline;
    private readonly int _triggerCooldownFrames;

    private SplineCompiledLevel[] _levels = Array.Empty<SplineCompiledLevel>();
    private int _currentLevelIndex;
    private bool _prefabsLoaded;
    private PrefabSimWorldUgcBakedAssets.BakedData _prefabSimBaked;
    private PrefabRenderWorldUgcBakedAssets.BakedData _prefabRenderBaked;
    private bool _needsRespawn;
    private Fixed64 _carryPlayerParamT;

    public SplineLevelLifecycleSystem(
        SplineSimContext simContext,
        SplineLevelSpawnContext spawnContext,
        SplineUiAssetPreloadContext uiAssetPreloadContext,
        [Tag("Spawn")] EcsSystemPipeline<PrefabSimWorld> spawnPipeline,
        [Tag("UiPreload")] EcsSystemPipeline<PrefabRenderWorld> uiAssetPreloadPipeline,
        [Arg("triggerCooldownFrames")] int triggerCooldownFrames)
    {
        _simContext = simContext;
        _spawnContext = spawnContext;
        _uiAssetPreloadContext = uiAssetPreloadContext;
        _spawnPipeline = spawnPipeline;
        _uiAssetPreloadPipeline = uiAssetPreloadPipeline;
        _triggerCooldownFrames = triggerCooldownFrames;
    }

    public PrefabSimWorld? SimWorld { get; private set; }
    public PrefabRenderWorld? RenderWorld { get; private set; }
    public bool PrefabsLoaded => _prefabsLoaded;
    public PrefabSimWorldUgcBakedAssets.BakedData PrefabSimBaked => _prefabSimBaked;
    public PrefabRenderWorldUgcBakedAssets.BakedData PrefabRenderBaked => _prefabRenderBaked;
    public int PrefabSpawnRequestedCount { get; private set; }
    public int PrefabSpawnSucceededCount { get; private set; }
    public int EnemyCount { get; private set; }
    public int TriggerCount { get; private set; }
    public int CurrentLevelIndex => _currentLevelIndex;
    public int LevelCount => _levels.Length;

    public bool TryGetCurrentLevel(out SplineCompiledLevel level)
    {
        if (_levels.Length <= 0 || (uint)_currentLevelIndex >= (uint)_levels.Length)
        {
            level = null!;
            return false;
        }

        level = _levels[_currentLevelIndex];
        return true;
    }

    public void SetCompiledLevels(SplineCompiledLevel[] levels, int targetLevelIndex)
    {
        _levels = levels ?? Array.Empty<SplineCompiledLevel>();
        if (_levels.Length <= 0)
        {
            _currentLevelIndex = 0;
            ResetWorldState();
            return;
        }

        _currentLevelIndex = targetLevelIndex;
        if ((uint)_currentLevelIndex >= (uint)_levels.Length)
        {
            _currentLevelIndex = 0;
        }

        _carryPlayerParamT = Fixed64.Zero;
        _needsRespawn = true;
    }

    public void SetPrefabBakedData(
        bool prefabsLoaded,
        in PrefabSimWorldUgcBakedAssets.BakedData simBaked,
        in PrefabRenderWorldUgcBakedAssets.BakedData renderBaked)
    {
        _prefabsLoaded = prefabsLoaded;
        _prefabSimBaked = simBaked;
        _prefabRenderBaked = renderBaked;

        if (_levels.Length > 0)
        {
            _needsRespawn = true;
        }
        else
        {
            ResetWorldState();
        }
    }

    public void Update()
    {
        if (_levels.Length <= 0)
        {
            return;
        }

        if (_simContext.TryConsumeTransition(out int targetLevelPk, out Fixed64 carryPlayerParamT))
        {
            QueueLevelByPk(targetLevelPk, carryPlayerParamT);
        }

        if (_needsRespawn)
        {
            RespawnCurrentLevel();
        }
    }

    private void QueueLevelByPk(int levelPk, Fixed64 carryPlayerParamT)
    {
        int targetLevelIndex = 0;
        for (int levelIndex = 0; levelIndex < _levels.Length; levelIndex++)
        {
            if (_levels[levelIndex].LevelId == levelPk)
            {
                targetLevelIndex = levelIndex;
                break;
            }
        }

        _currentLevelIndex = targetLevelIndex;
        _carryPlayerParamT = carryPlayerParamT;
        _needsRespawn = true;
    }

    private void RespawnCurrentLevel()
    {
        _needsRespawn = false;

        if (_levels.Length <= 0 || (uint)_currentLevelIndex >= (uint)_levels.Length)
        {
            ResetWorldState();
            return;
        }

        SplineCompiledLevel level = _levels[_currentLevelIndex];
        _simContext.SetLevelPoints(level.Points);
        _simContext.ResetMatchOutcome();
        _simContext.PlayerEntity = EntityHandle.Invalid;
        _simContext.TriggerCooldownFrames = _triggerCooldownFrames;
        _simContext.ClearProjectileSpawnRequests();

        if (!_prefabsLoaded)
        {
            ResetWorldState();
            return;
        }

        SimWorld = new PrefabSimWorld();
        RenderWorld = new PrefabRenderWorld();
        PrefabSimWorldUgcBakedAssets.ApplyBakedData(_prefabSimBaked, SimWorld);
        PrefabRenderWorldUgcBakedAssets.ApplyBakedData(_prefabRenderBaked, RenderWorld);

        _spawnContext.BeginSpawn(
            _levels,
            _currentLevelIndex,
            _carryPlayerParamT,
            RenderWorld,
            _prefabSimBaked,
            _prefabRenderBaked);
        _spawnPipeline.RunFrame(SimWorld);

        PrefabSpawnRequestedCount = _spawnContext.SpawnRequestedCount;
        PrefabSpawnSucceededCount = _spawnContext.SpawnSucceededCount;
        _simContext.PlayerEntity = _spawnContext.PlayerEntity;

        EnemyCount = _spawnContext.EnemyCount;
        TriggerCount = _spawnContext.TriggerCount;

        _uiAssetPreloadContext.RequestPreload();
        _uiAssetPreloadPipeline.RunFrame(RenderWorld);
    }

    private void ResetWorldState()
    {
        SimWorld = null;
        RenderWorld = null;
        PrefabSpawnRequestedCount = 0;
        PrefabSpawnSucceededCount = 0;
        EnemyCount = 0;
        TriggerCount = 0;
        _simContext.PlayerEntity = EntityHandle.Invalid;
        _simContext.ResetMatchOutcome();
        _simContext.MoveAxis = 0;
        _simContext.FireHeld = false;
        _simContext.WeaponSwitchDelta = 0;
        _simContext.ClearProjectileSpawnRequests();
    }
}
