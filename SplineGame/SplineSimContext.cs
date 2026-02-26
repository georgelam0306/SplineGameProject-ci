using DerpLib.Ecs;
using FixedMath;

namespace SplineGame;

public sealed class SplineSimContext
{
    private const int MaxProjectileSpawnRequests = 256;
    private readonly ProjectileSpawnRequest[] _projectileSpawnRequests = new ProjectileSpawnRequest[MaxProjectileSpawnRequests];

    public SplineCompiledLevel.Point[] Points { get; private set; } = Array.Empty<SplineCompiledLevel.Point>();

    public EntityHandle PlayerEntity { get; set; } = EntityHandle.Invalid;
    public int MoveAxis { get; set; }
    public bool FireHeld { get; set; }
    public int WeaponSwitchDelta { get; set; }
    public Fixed64 DeltaTime { get; set; }
    public Fixed64 PlayerWorldSpeed { get; set; }
    public Fixed64 EnemyWorldSpeedScale { get; set; }
    public int TriggerCooldownFrames { get; set; }
    public SplineMatchOutcome MatchOutcome { get; private set; }

    public int ProjectileSpawnRequestCount { get; private set; }

    public bool TransitionRequested { get; private set; }
    public int TransitionTargetLevelPk { get; private set; }
    public Fixed64 TransitionCarryPlayerParamT { get; private set; }

    public ReadOnlySpan<ProjectileSpawnRequest> ProjectileSpawnRequests =>
        _projectileSpawnRequests.AsSpan(0, ProjectileSpawnRequestCount);

    public void SetLevelPoints(SplineCompiledLevel.Point[] points)
    {
        Points = points ?? Array.Empty<SplineCompiledLevel.Point>();
    }

    public void ResetMatchOutcome()
    {
        MatchOutcome = SplineMatchOutcome.Running;
    }

    public void SetMatchOutcome(SplineMatchOutcome matchOutcome)
    {
        if (MatchOutcome != SplineMatchOutcome.Running)
        {
            return;
        }

        MatchOutcome = matchOutcome;
    }

    public void RequestTransition(int targetLevelPk, Fixed64 carryPlayerParamT)
    {
        TransitionRequested = true;
        TransitionTargetLevelPk = targetLevelPk;
        TransitionCarryPlayerParamT = carryPlayerParamT;
    }

    public bool TryQueueProjectileSpawn(
        int projectileRowId,
        Fixed64 spawnX,
        Fixed64 spawnY,
        Fixed64 velocityX,
        Fixed64 velocityY,
        bool isEnemyProjectile)
    {
        if ((uint)ProjectileSpawnRequestCount >= (uint)_projectileSpawnRequests.Length)
        {
            return false;
        }

        _projectileSpawnRequests[ProjectileSpawnRequestCount] = new ProjectileSpawnRequest(
            projectileRowId,
            spawnX,
            spawnY,
            velocityX,
            velocityY,
            isEnemyProjectile);
        ProjectileSpawnRequestCount++;
        return true;
    }

    public void ClearProjectileSpawnRequests()
    {
        ProjectileSpawnRequestCount = 0;
    }

    public bool TryConsumeTransition(out int targetLevelPk, out Fixed64 carryPlayerParamT)
    {
        if (!TransitionRequested)
        {
            targetLevelPk = 0;
            carryPlayerParamT = Fixed64.Zero;
            return false;
        }

        TransitionRequested = false;
        targetLevelPk = TransitionTargetLevelPk;
        carryPlayerParamT = TransitionCarryPlayerParamT;
        TransitionTargetLevelPk = 0;
        TransitionCarryPlayerParamT = Fixed64.Zero;
        return true;
    }

    public readonly struct ProjectileSpawnRequest
    {
        public ProjectileSpawnRequest(
            int projectileRowId,
            Fixed64 spawnX,
            Fixed64 spawnY,
            Fixed64 velocityX,
            Fixed64 velocityY,
            bool isEnemyProjectile)
        {
            ProjectileRowId = projectileRowId;
            SpawnX = spawnX;
            SpawnY = spawnY;
            VelocityX = velocityX;
            VelocityY = velocityY;
            IsEnemyProjectile = isEnemyProjectile;
        }

        public int ProjectileRowId { get; }
        public Fixed64 SpawnX { get; }
        public Fixed64 SpawnY { get; }
        public Fixed64 VelocityX { get; }
        public Fixed64 VelocityY { get; }
        public bool IsEnemyProjectile { get; }
    }
}
