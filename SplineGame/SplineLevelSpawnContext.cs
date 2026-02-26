using DerpDocDatabase;
using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;
using FixedMath;

namespace SplineGame;

public sealed class SplineLevelSpawnContext
{
    public SplineLevelSpawnContext(GameDatabase database)
    {
        Database = database;
    }

    public GameDatabase Database { get; }
    public SplineCompiledLevel[] Levels { get; private set; } = Array.Empty<SplineCompiledLevel>();
    public int CurrentLevelIndex { get; private set; }
    public Fixed64 CarryPlayerParamT { get; private set; }
    public PrefabRenderWorld? RenderWorld { get; private set; }
    public PrefabSimWorldUgcBakedAssets.BakedData SimBaked { get; private set; }
    public PrefabRenderWorldUgcBakedAssets.BakedData RenderBaked { get; private set; }

    public bool SpawnRequested { get; private set; }
    public EntityHandle PlayerEntity { get; set; } = EntityHandle.Invalid;
    public int SpawnRequestedCount { get; set; }
    public int SpawnSucceededCount { get; set; }
    public int EnemyCount { get; set; }
    public int TriggerCount { get; set; }

    public void BeginSpawn(
        SplineCompiledLevel[] levels,
        int currentLevelIndex,
        Fixed64 carryPlayerParamT,
        PrefabRenderWorld renderWorld,
        in PrefabSimWorldUgcBakedAssets.BakedData simBaked,
        in PrefabRenderWorldUgcBakedAssets.BakedData renderBaked)
    {
        Levels = levels;
        CurrentLevelIndex = currentLevelIndex;
        CarryPlayerParamT = carryPlayerParamT;
        RenderWorld = renderWorld;
        SimBaked = simBaked;
        RenderBaked = renderBaked;

        SpawnRequested = true;
        PlayerEntity = EntityHandle.Invalid;
        SpawnRequestedCount = 0;
        SpawnSucceededCount = 0;
        EnemyCount = 0;
        TriggerCount = 0;
    }

    public void CompleteSpawn()
    {
        SpawnRequested = false;
    }
}
