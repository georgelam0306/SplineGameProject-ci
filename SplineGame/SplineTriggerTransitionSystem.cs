using DerpDocDatabase.Prefabs;
using DerpLib.DI;
using DerpLib.Ecs;
using FixedMath;

namespace SplineGame;

public sealed class SplineTriggerTransitionSystem : IEcsSystem<PrefabSimWorld>
{
    private readonly SplineSimContext _context;
    private readonly float _playerCollisionRadius;
    private readonly int _cooldownFrames;

    public SplineTriggerTransitionSystem(
        SplineSimContext context,
        [Arg("playerCollisionRadius")] float playerCollisionRadius,
        [Arg("triggerCooldownFrames")] int cooldownFrames)
    {
        _context = context;
        _playerCollisionRadius = playerCollisionRadius;
        _cooldownFrames = cooldownFrames;
    }

    public void Update(PrefabSimWorld world)
    {
        if (_context.MatchOutcome != SplineMatchOutcome.Running)
        {
            return;
        }

        if (_context.TriggerCooldownFrames > 0)
        {
            _context.TriggerCooldownFrames--;
            return;
        }

        EntityHandle playerEntity = _context.PlayerEntity;
        if (!playerEntity.IsValid)
        {
            return;
        }

        var playerShape = world.Player;
        if (!playerShape.TryGetRow(playerEntity, out int playerRow))
        {
            return;
        }

        Fixed64 playerParamT = playerShape.SplineTransform(playerRow).ParamT;
        SplineMath.SamplePositionAndTangent(
            _context.Points,
            playerParamT.ToFloat(),
            out float playerX,
            out float playerY,
            out _,
            out _);

        var triggerShape = world.RoomTrigger;
        for (int triggerRow = 0; triggerRow < triggerShape.Count; triggerRow++)
        {
            ref readonly SplineTransformComponent triggerTransform = ref triggerShape.SplineTransform(triggerRow);
            ref readonly SplineTriggerComponent trigger = ref triggerShape.SplineTrigger(triggerRow);

            SplineMath.SamplePositionAndTangent(
                _context.Points,
                triggerTransform.ParamT.ToFloat(),
                out float triggerX,
                out float triggerY,
                out _,
                out _);

            float deltaX = playerX - triggerX;
            float deltaY = playerY - triggerY;
            float triggerRadius = trigger.Radius.ToFloat() + _playerCollisionRadius;
            float distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared > (triggerRadius * triggerRadius))
            {
                continue;
            }

            int targetLevelPk = trigger.TargetLevelPk.ToInt();
            _context.TriggerCooldownFrames = _cooldownFrames;
            _context.RequestTransition(targetLevelPk, playerParamT);
            return;
        }
    }
}
