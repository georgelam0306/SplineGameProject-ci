using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;
using FixedMath;

namespace SplineGame;

public sealed class SplineProjectileHitSystem : IEcsSystem<PrefabSimWorld>
{
    private const int DamageFlashFrames = 45;
    private const float MinimumProjectileRadius = 4f;
    private const float EnemyCollisionRadius = 20f;

    private readonly SplineSimContext _context;
    private readonly SplineLevelLifecycleSystem _levelLifecycleSystem;

    public SplineProjectileHitSystem(SplineSimContext context, SplineLevelLifecycleSystem levelLifecycleSystem)
    {
        _context = context;
        _levelLifecycleSystem = levelLifecycleSystem;
    }

    public void Update(PrefabSimWorld world)
    {
        if (_context.Points.Length < 2)
        {
            return;
        }

        PrefabRenderWorld? renderWorld = _levelLifecycleSystem.RenderWorld;

        var projectileShape = world.Projectile;
        var enemyShape = world.BaseEnemy;
        var playerShape = world.Player;

        bool hasPlayer = false;
        int playerRow = -1;
        float playerX = 0f;
        float playerY = 0f;
        EntityHandle playerEntity = _context.PlayerEntity;
        if (playerEntity.IsValid && playerShape.TryGetRow(playerEntity, out int resolvedPlayerRow))
        {
            hasPlayer = true;
            playerRow = resolvedPlayerRow;

            ref readonly SplineTransformComponent playerTransform = ref playerShape.SplineTransform(playerRow);
            SplineMath.SamplePositionAndTangent(
                _context.Points,
                playerTransform.ParamT.ToFloat(),
                out playerX,
                out playerY,
                out _,
                out _);
        }

        for (int projectileRow = 0; projectileRow < projectileShape.Count; projectileRow++)
        {
            EntityHandle projectileEntity = projectileShape.Entity(projectileRow);
            ref readonly SplineProjectileComponent projectile = ref projectileShape.SplineProjectile(projectileRow);

            float projectileX = projectile.PositionX.ToFloat();
            float projectileY = projectile.PositionY.ToFloat();
            float projectileRadius = projectile.Radius.ToFloat();
            if (!float.IsFinite(projectileRadius) || projectileRadius < MinimumProjectileRadius)
            {
                projectileRadius = MinimumProjectileRadius;
            }

            if (projectile.IsEnemyProjectile)
            {
                if (!hasPlayer)
                {
                    continue;
                }

                float playerCollisionRadius = projectileRadius + SplineGameFrameContext.PlayerCollisionRadius;
                if (!IsWithinCollisionRadius(projectileX, projectileY, playerX, playerY, playerCollisionRadius))
                {
                    continue;
                }

                ref SplineHealthComponent playerHealth = ref playerShape.SplineHealth(playerRow);
                playerHealth.Hp -= projectile.Damage;
                playerHealth.DamageFlashFramesRemaining = Fixed64.FromInt(DamageFlashFrames);
                if (playerHealth.Hp <= Fixed64.Zero)
                {
                    SplineEntityDestroyHelper.QueueDestroySimAndRender(world, renderWorld, playerEntity);
                    _context.PlayerEntity = EntityHandle.Invalid;
                    hasPlayer = false;
                    playerEntity = EntityHandle.Invalid;
                }

                SplineEntityDestroyHelper.QueueDestroySimAndRender(world, renderWorld, projectileEntity);
                continue;
            }

            for (int enemyRow = 0; enemyRow < enemyShape.Count; enemyRow++)
            {
                ref readonly SplineTransformComponent enemyTransform = ref enemyShape.SplineTransform(enemyRow);
                SplineMath.SamplePositionAndTangent(
                    _context.Points,
                    enemyTransform.ParamT.ToFloat(),
                    out float enemyX,
                    out float enemyY,
                    out _,
                    out _);

                float collisionRadius = projectileRadius + EnemyCollisionRadius;
                if (!IsWithinCollisionRadius(projectileX, projectileY, enemyX, enemyY, collisionRadius))
                {
                    continue;
                }

                ref SplineHealthComponent enemyHealth = ref enemyShape.SplineHealth(enemyRow);
                enemyHealth.Hp -= projectile.Damage;
                enemyHealth.DamageFlashFramesRemaining = Fixed64.FromInt(DamageFlashFrames);

                EntityHandle enemyEntity = enemyShape.Entity(enemyRow);
                if (enemyHealth.Hp <= Fixed64.Zero)
                {
                    SplineEntityDestroyHelper.QueueDestroySimAndRender(world, renderWorld, enemyEntity);
                }

                SplineEntityDestroyHelper.QueueDestroySimAndRender(world, renderWorld, projectileEntity);
                break;
            }
        }
    }

    private static bool IsWithinCollisionRadius(float x1, float y1, float x2, float y2, float collisionRadius)
    {
        float deltaX = x1 - x2;
        float deltaY = y1 - y2;
        float distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
        return distanceSquared <= (collisionRadius * collisionRadius);
    }
}
