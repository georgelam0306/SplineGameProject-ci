using DerpDocDatabase;
using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;
using FixedMath;

namespace SplineGame;

public sealed class SplineLevelSpawnSystem : IEcsSystem<PrefabSimWorld>
{
    private const float MinimumTriggerRadius = 16f;

    private readonly SplineLevelSpawnContext _context;

    public SplineLevelSpawnSystem(SplineLevelSpawnContext context)
    {
        _context = context;
    }

    public void Update(PrefabSimWorld simWorld)
    {
        if (!_context.SpawnRequested)
        {
            return;
        }

        PrefabRenderWorld? renderWorld = _context.RenderWorld;
        if (renderWorld == null)
        {
            _context.CompleteSpawn();
            return;
        }

        if (_context.Levels.Length <= 0 || (uint)_context.CurrentLevelIndex >= (uint)_context.Levels.Length)
        {
            _context.CompleteSpawn();
            return;
        }

        ref readonly SplineCompiledLevel level = ref _context.Levels[_context.CurrentLevelIndex];
        GameLevelsSplineGameLevelEntitiesTable.ParentScope entitiesScope =
            _context.Database.GameLevelsSplineGameLevel.FindByIdView(level.SplineRowId).Entities;

        SpawnPlayer(simWorld, renderWorld, entitiesScope);
        SpawnEnemies(simWorld, renderWorld, entitiesScope);
        SpawnTriggers(simWorld, renderWorld, entitiesScope);

        _context.EnemyCount = simWorld.BaseEnemy.Count;
        _context.TriggerCount = simWorld.RoomTrigger.Count;
        _context.CompleteSpawn();
    }

    private void SpawnPlayer(
        PrefabSimWorld simWorld,
        PrefabRenderWorld renderWorld,
        GameLevelsSplineGameLevelEntitiesTable.ParentScope entitiesScope)
    {
        int bestPlayerOrder = int.MaxValue;
        Fixed64 bestParamT = Fixed64.Zero;
        DerpDocPrefabCell bestPrefab = default;
        int bestPlayerMaxHealth = 1;
        int bestStartingWeapon = -1;

        DerpDocPrefabCell fallbackPlayerPrefab = default;
        int fallbackPlayerMaxHealth = 1;
        int fallbackStartingWeapon = -1;
        if (_context.Database.Player.Count > 0 && _context.Database.Player.TryFindById(0, out Player fallbackPlayerDefinition))
        {
            fallbackPlayerPrefab = fallbackPlayerDefinition.Prefab;
            fallbackPlayerMaxHealth = fallbackPlayerDefinition.MaxHealth;
            fallbackStartingWeapon = fallbackPlayerDefinition.StartingWeapon;
        }

        int playerCount = _context.Database.Player.Count;
        for (int playerId = 0; playerId < playerCount; playerId++)
        {
            if (!_context.Database.Player.TryFindById(playerId, out Player playerDefinition))
            {
                continue;
            }

            if (!entitiesScope.Player.TryFindById(playerId, out GameLevelsSplineGameLevelEntitiesTable.ParentScopedRange placements))
            {
                continue;
            }

            var placementEnumerator = placements.GetEnumerator();
            while (placementEnumerator.MoveNext())
            {
                ref readonly GameLevelsSplineGameLevelEntities placementRow = ref placementEnumerator.Current;
                if (placementRow.Order >= bestPlayerOrder)
                {
                    continue;
                }

                bestPlayerOrder = placementRow.Order;
                bestParamT = SplineMath.Wrap01(placementRow.ParamT);
                bestPrefab = ResolvePlacementPrefab(placementRow, playerDefinition.Prefab);
                bestPlayerMaxHealth = playerDefinition.MaxHealth;
                bestStartingWeapon = playerDefinition.StartingWeapon;
            }
        }

        if (bestPlayerOrder == int.MaxValue)
        {
            bestParamT = SplineMath.Wrap01(_context.CarryPlayerParamT);
            bestPrefab = fallbackPlayerPrefab;
            bestPlayerMaxHealth = fallbackPlayerMaxHealth;
            bestStartingWeapon = fallbackStartingWeapon;
        }

        if (bestPrefab.PrefabId == 0)
        {
            return;
        }

        _context.SpawnRequestedCount++;
        if (!DerpDocPrefabSpawner.TrySpawn(
                _context.SimBaked,
                simWorld,
                _context.RenderBaked,
                renderWorld,
                bestPrefab,
                out EntityHandle simEntity,
                out _))
        {
            return;
        }

        _context.SpawnSucceededCount++;
        if (!simWorld.Player.TryGetRow(simEntity, out int playerTransformRow))
        {
            return;
        }

        ref SplineTransformComponent playerTransform = ref simWorld.Player.SplineTransform(playerTransformRow);
        playerTransform.ParamT = bestParamT;

        ref SplineHealthComponent playerHealth = ref simWorld.Player.SplineHealth(playerTransformRow);
        Fixed64 resolvedPlayerMaxHealth = Fixed64.FromInt(ResolvePositiveHealth(bestPlayerMaxHealth));
        playerHealth.Hp = resolvedPlayerMaxHealth;
        playerHealth.MaxHp = resolvedPlayerMaxHealth;
        playerHealth.DamageFlashFramesRemaining = Fixed64.Zero;

        ref SplineWeaponInventoryComponent playerWeaponInventory = ref simWorld.Player.SplineWeaponInventory(playerTransformRow);
        int startingWeaponSlot = ResolveInitialWeaponSlot(playerWeaponInventory.WeaponIds, bestStartingWeapon, simWorld.VarHeap.Bytes);
        playerWeaponInventory.ActiveSlot = Fixed64.FromInt(startingWeaponSlot);
        playerWeaponInventory.CooldownFramesRemaining = Fixed64.Zero;

        _context.PlayerEntity = simEntity;
    }

    private void SpawnEnemies(
        PrefabSimWorld simWorld,
        PrefabRenderWorld renderWorld,
        GameLevelsSplineGameLevelEntitiesTable.ParentScope entitiesScope)
    {
        int enemyCount = _context.Database.Enemies.Count;
        for (int enemyId = 0; enemyId < enemyCount; enemyId++)
        {
            if (!_context.Database.Enemies.TryFindById(enemyId, out Enemies enemyRow))
            {
                continue;
            }

            if (!entitiesScope.Enemies.TryFindById(enemyId, out GameLevelsSplineGameLevelEntitiesTable.ParentScopedRange placements))
            {
                continue;
            }

            var placementEnumerator = placements.GetEnumerator();
            while (placementEnumerator.MoveNext())
            {
                ref readonly GameLevelsSplineGameLevelEntities placementRow = ref placementEnumerator.Current;
                DerpDocPrefabCell prefab = enemyRow.Prefab;
                if (prefab.PrefabId == 0)
                {
                    continue;
                }

                _context.SpawnRequestedCount++;
                if (!DerpDocPrefabSpawner.TrySpawn(
                        _context.SimBaked,
                        simWorld,
                        _context.RenderBaked,
                        renderWorld,
                        prefab,
                        out EntityHandle simEntity,
                        out _))
                {
                    continue;
                }

                _context.SpawnSucceededCount++;
                if (simWorld.BaseEnemy.TryGetRow(simEntity, out int enemyRowIndex))
                {
                    ref SplineTransformComponent transform = ref simWorld.BaseEnemy.SplineTransform(enemyRowIndex);
                    ref SplineEnemyMoveComponent move = ref simWorld.BaseEnemy.SplineEnemyMove(enemyRowIndex);
                    ref SplineHealthComponent health = ref simWorld.BaseEnemy.SplineHealth(enemyRowIndex);
                    transform.ParamT = SplineMath.Wrap01(placementRow.ParamT);
                    move.Speed = Fixed64.Zero;
                    move.FireRateFrames = Fixed64.FromInt(ResolvePositiveFireRate(enemyRow.FireRate));
                    move.FireCooldownFramesRemaining = Fixed64.Zero;

                    Fixed64 resolvedEnemyHealth = Fixed64.FromInt(ResolvePositiveHealth(enemyRow.Health));
                    health.Hp = resolvedEnemyHealth;
                    health.MaxHp = resolvedEnemyHealth;
                    health.DamageFlashFramesRemaining = Fixed64.Zero;
                }
            }
        }
    }

    private void SpawnTriggers(
        PrefabSimWorld simWorld,
        PrefabRenderWorld renderWorld,
        GameLevelsSplineGameLevelEntitiesTable.ParentScope entitiesScope)
    {
        int triggerCount = _context.Database.Triggers.Count;
        for (int triggerId = 0; triggerId < triggerCount; triggerId++)
        {
            if (!_context.Database.Triggers.TryFindById(triggerId, out Triggers triggerRow))
            {
                continue;
            }

            if (!entitiesScope.Triggers.TryFindById(triggerId, out GameLevelsSplineGameLevelEntitiesTable.ParentScopedRange placements))
            {
                continue;
            }

            Fixed64 triggerRadius = Fixed64.FromFloat(ResolveRadius(triggerRow.Scale, MinimumTriggerRadius));
            var placementEnumerator = placements.GetEnumerator();
            while (placementEnumerator.MoveNext())
            {
                ref readonly GameLevelsSplineGameLevelEntities placementRow = ref placementEnumerator.Current;
                DerpDocPrefabCell prefab = ResolvePlacementPrefab(placementRow, triggerRow.Prefab);
                if (prefab.PrefabId == 0)
                {
                    continue;
                }

                int targetLevelPk = ResolveTargetLevelPk(placementRow, ResolveDefaultTargetLevelPk());

                _context.SpawnRequestedCount++;
                if (!DerpDocPrefabSpawner.TrySpawn(
                        _context.SimBaked,
                        simWorld,
                        _context.RenderBaked,
                        renderWorld,
                        prefab,
                        out EntityHandle simEntity,
                        out _))
                {
                    continue;
                }

                _context.SpawnSucceededCount++;
                if (simWorld.RoomTrigger.TryGetRow(simEntity, out int triggerRowIndex))
                {
                    ref SplineTransformComponent transform = ref simWorld.RoomTrigger.SplineTransform(triggerRowIndex);
                    ref SplineTriggerComponent trigger = ref simWorld.RoomTrigger.SplineTrigger(triggerRowIndex);
                    transform.ParamT = SplineMath.Wrap01(placementRow.ParamT);
                    trigger.Radius = triggerRadius;
                    trigger.TargetLevelPk = Fixed64.FromInt(targetLevelPk);
                }
            }
        }
    }

    private int ResolveDefaultTargetLevelPk()
    {
        if (_context.Levels.Length <= 0)
        {
            return 0;
        }

        int nextLevelIndex = _context.CurrentLevelIndex + 1;
        if ((uint)nextLevelIndex >= (uint)_context.Levels.Length)
        {
            nextLevelIndex = 0;
        }

        return _context.Levels[nextLevelIndex].LevelId;
    }

    private static DerpDocPrefabCell ResolvePlacementPrefab(in GameLevelsSplineGameLevelEntities placementRow, DerpDocPrefabCell fallback)
    {
        DerpDocPrefabCell placementPrefab = placementRow.Prefab;
        return placementPrefab.PrefabId != 0 ? placementPrefab : fallback;
    }

    private static int ResolveTargetLevelPk(in GameLevelsSplineGameLevelEntities placementRow, int fallbackLevelPk)
    {
        int targetLevelPk = placementRow.TargetLevel;
        return targetLevelPk >= 0 ? targetLevelPk : fallbackLevelPk;
    }

    private static float ResolveRadius(Fixed64 scaleValue, float minimumRadius)
    {
        float scale = MathF.Abs(scaleValue.ToFloat());
        float radius = scale * 128f;
        if (radius < minimumRadius)
        {
            radius = minimumRadius;
        }

        return radius;
    }

    private static int ResolvePositiveHealth(int configuredHealth)
    {
        return configuredHealth > 0 ? configuredHealth : 1;
    }

    private static int ResolvePositiveFireRate(int configuredFireRate)
    {
        return configuredFireRate > 0 ? configuredFireRate : 1;
    }

    private static int ResolveInitialWeaponSlot(
        in ListHandle<int> weaponIds,
        int startingWeaponId,
        ReadOnlySpan<byte> heapBytes)
    {
        var weapons = new ResizableReadOnlyView<int>(heapBytes, in weaponIds);
        int weaponCount = weapons.Count;
        if (weaponCount <= 0)
        {
            return 0;
        }

        if (startingWeaponId < 0)
        {
            return 0;
        }

        for (int weaponIndex = 0; weaponIndex < weaponCount; weaponIndex++)
        {
            if (weapons[weaponIndex] == startingWeaponId)
            {
                return weaponIndex;
            }
        }

        return 0;
    }
}
