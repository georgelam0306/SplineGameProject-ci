using System.Numerics;
using DerpDocDatabase;
using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;
using FixedMath;

namespace SplineGame;

public sealed class SplineProjectileSpawnSystem : IEcsSystem<PrefabSimWorld>
{
    private const float MinimumVelocityMagnitudeSquared = 0.0001f;

    private readonly GameDatabase _database;
    private readonly SplineSimContext _context;
    private readonly SplineLevelLifecycleSystem _levelLifecycleSystem;

    public SplineProjectileSpawnSystem(
        GameDatabase database,
        SplineSimContext context,
        SplineLevelLifecycleSystem levelLifecycleSystem)
    {
        _database = database;
        _context = context;
        _levelLifecycleSystem = levelLifecycleSystem;
    }

    public void Update(PrefabSimWorld world)
    {
        if (_context.ProjectileSpawnRequestCount <= 0)
        {
            return;
        }

        PrefabRenderWorld? renderWorld = _levelLifecycleSystem.RenderWorld;
        if (!_levelLifecycleSystem.PrefabsLoaded || renderWorld == null)
        {
            _context.ClearProjectileSpawnRequests();
            return;
        }

        ReadOnlySpan<SplineSimContext.ProjectileSpawnRequest> spawnRequests = _context.ProjectileSpawnRequests;
        for (int requestIndex = 0; requestIndex < spawnRequests.Length; requestIndex++)
        {
            ref readonly SplineSimContext.ProjectileSpawnRequest request = ref spawnRequests[requestIndex];
            if (request.ProjectileRowId < 0)
            {
                continue;
            }

            if (!_database.Projectiles.TryFindById(request.ProjectileRowId, out Projectiles projectileDefinition))
            {
                continue;
            }

            DerpDocPrefabCell prefab = projectileDefinition.Prefab;
            if (prefab.PrefabId == 0)
            {
                continue;
            }

            if (!DerpDocPrefabSpawner.TrySpawn(
                    _levelLifecycleSystem.PrefabSimBaked,
                    world,
                    _levelLifecycleSystem.PrefabRenderBaked,
                    renderWorld,
                    prefab,
                    out EntityHandle projectileEntity,
                    out _))
            {
                continue;
            }

            if (!world.Projectile.TryGetRow(projectileEntity, out int projectileRow))
            {
                continue;
            }

            ref SplineTransformComponent transform = ref world.Projectile.SplineTransform(projectileRow);
            ref SplineProjectileComponent projectile = ref world.Projectile.SplineProjectile(projectileRow);

            Vector2 velocity = new(request.VelocityX.ToFloat(), request.VelocityY.ToFloat());
            float velocityLengthSquared = velocity.LengthSquared();
            if (velocityLengthSquared <= MinimumVelocityMagnitudeSquared)
            {
                velocity = new Vector2(0f, -1f);
            }
            else
            {
                velocity /= MathF.Sqrt(velocityLengthSquared);
            }

            transform.ParamT = Fixed64.Zero;
            projectile.Radius = projectileDefinition.Radius;
            projectile.Speed = projectileDefinition.Speed;
            projectile.LifetimeFrames = Fixed64.FromInt(ResolvePositiveInt(projectileDefinition.LifetimeFrames, fallbackValue: 1));
            projectile.Damage = Fixed64.FromInt(ResolvePositiveInt(projectileDefinition.Damage, fallbackValue: 1));
            projectile.PositionX = request.SpawnX;
            projectile.PositionY = request.SpawnY;
            projectile.VelocityX = Fixed64.FromFloat(velocity.X);
            projectile.VelocityY = Fixed64.FromFloat(velocity.Y);
            projectile.IsEnemyProjectile = request.IsEnemyProjectile;
        }

        _context.ClearProjectileSpawnRequests();
    }

    private static int ResolvePositiveInt(int value, int fallbackValue)
    {
        return value > 0 ? value : fallbackValue;
    }
}
