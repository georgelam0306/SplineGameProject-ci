using DerpDocDatabase;
using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;
using FixedMath;
using System.Numerics;

namespace SplineGame;

public sealed class SplineEnemyFireSystem : IEcsSystem<PrefabSimWorld>
{
    private const float MinimumDirectionMagnitudeSquared = 0.0001f;

    private readonly GameDatabase _database;
    private readonly SplineSimContext _context;

    public SplineEnemyFireSystem(GameDatabase database, SplineSimContext context)
    {
        _database = database;
        _context = context;
    }

    public void Update(PrefabSimWorld world)
    {
        if (_context.Points.Length < 2)
        {
            return;
        }

        int projectileRowId = ResolveDefaultProjectileRowId();
        if (projectileRowId < 0)
        {
            return;
        }

        Vector2 splineCentroid = SplineMath.ComputeCentroid(_context.Points);

        var enemyShape = world.BaseEnemy;
        for (int enemyRow = 0; enemyRow < enemyShape.Count; enemyRow++)
        {
            ref SplineEnemyMoveComponent move = ref enemyShape.SplineEnemyMove(enemyRow);
            if (move.FireCooldownFramesRemaining > Fixed64.Zero)
            {
                move.FireCooldownFramesRemaining -= Fixed64.OneValue;
                if (move.FireCooldownFramesRemaining < Fixed64.Zero)
                {
                    move.FireCooldownFramesRemaining = Fixed64.Zero;
                }

                continue;
            }

            int fireRateFrames = move.FireRateFrames.ToInt();
            if (fireRateFrames < 1)
            {
                fireRateFrames = 1;
            }

            ref readonly SplineTransformComponent enemyTransform = ref enemyShape.SplineTransform(enemyRow);
            SplineMath.SamplePositionAndTangent(
                _context.Points,
                enemyTransform.ParamT.ToFloat(),
                out float enemyX,
                out float enemyY,
                out float tangentX,
                out float tangentY);

            Vector2 enemyWorldPosition = new(enemyX, enemyY);
            Vector2 tangentDirection = new(tangentX, tangentY);
            Vector2 forwardDirection;
            if (!SplineMath.TryResolveInwardDirection(enemyWorldPosition, tangentDirection, splineCentroid, out forwardDirection))
            {
                forwardDirection = new Vector2(0f, -1f);
            }

            float forwardLengthSquared = forwardDirection.LengthSquared();
            if (forwardLengthSquared <= MinimumDirectionMagnitudeSquared)
            {
                forwardDirection = new Vector2(0f, -1f);
            }
            else
            {
                float inverseForwardLength = 1f / MathF.Sqrt(forwardLengthSquared);
                forwardDirection *= inverseForwardLength;
            }

            if (!_context.TryQueueProjectileSpawn(
                    projectileRowId,
                    spawnX: Fixed64.FromFloat(enemyX),
                    spawnY: Fixed64.FromFloat(enemyY),
                    velocityX: Fixed64.FromFloat(forwardDirection.X),
                    velocityY: Fixed64.FromFloat(forwardDirection.Y),
                    isEnemyProjectile: true))
            {
                continue;
            }

            move.FireCooldownFramesRemaining = Fixed64.FromInt(fireRateFrames);
        }
    }

    private int ResolveDefaultProjectileRowId()
    {
        int projectileCount = _database.Projectiles.Count;
        for (int projectileRowId = 0; projectileRowId < projectileCount; projectileRowId++)
        {
            if (_database.Projectiles.TryFindById(projectileRowId, out Projectiles projectileRow) && projectileRow.Prefab.PrefabId != 0)
            {
                return projectileRowId;
            }
        }

        return -1;
    }
}
