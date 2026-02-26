using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;
using FixedMath;

namespace SplineGame;

public sealed class SplinePlayerMoveSystem : IEcsSystem<PrefabSimWorld>
{
    private readonly SplineSimContext _context;

    public SplinePlayerMoveSystem(SplineSimContext context)
    {
        _context = context;
    }

    public void Update(PrefabSimWorld world)
    {
        if (_context.MoveAxis == 0)
        {
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

        ref SplineTransformComponent transform = ref playerShape.SplineTransform(playerRow);
        Fixed64 deltaDistance = _context.PlayerWorldSpeed * _context.DeltaTime * Fixed64.FromInt(_context.MoveAxis);
        transform.ParamT = SplineMath.AdvanceParamByWorldDistance(_context.Points, transform.ParamT, deltaDistance);
    }
}
