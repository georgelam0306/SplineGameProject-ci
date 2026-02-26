using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;
using FixedMath;

namespace SplineGame;

public sealed class SplineProjectileMoveSystem : IEcsSystem<PrefabSimWorld>
{
    private readonly SplineSimContext _context;
    private readonly SplineLevelLifecycleSystem _levelLifecycleSystem;

    public SplineProjectileMoveSystem(SplineSimContext context, SplineLevelLifecycleSystem levelLifecycleSystem)
    {
        _context = context;
        _levelLifecycleSystem = levelLifecycleSystem;
    }

    public void Update(PrefabSimWorld world)
    {
        PrefabRenderWorld? renderWorld = _levelLifecycleSystem.RenderWorld;

        var projectileShape = world.Projectile;
        for (int projectileRow = 0; projectileRow < projectileShape.Count; projectileRow++)
        {
            EntityHandle projectileEntity = projectileShape.Entity(projectileRow);
            ref SplineProjectileComponent projectile = ref projectileShape.SplineProjectile(projectileRow);

            Fixed64 deltaDistance = projectile.Speed * _context.EnemyWorldSpeedScale * _context.DeltaTime;
            projectile.PositionX += projectile.VelocityX * deltaDistance;
            projectile.PositionY += projectile.VelocityY * deltaDistance;

            projectile.LifetimeFrames -= Fixed64.OneValue;
            if (projectile.LifetimeFrames <= Fixed64.Zero)
            {
                SplineEntityDestroyHelper.QueueDestroySimAndRender(world, renderWorld, projectileEntity);
            }
        }
    }
}
