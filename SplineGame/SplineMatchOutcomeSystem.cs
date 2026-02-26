using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;

namespace SplineGame;

public sealed class SplineMatchOutcomeSystem : IEcsSystem<PrefabSimWorld>
{
    private readonly SplineSimContext _context;

    public SplineMatchOutcomeSystem(SplineSimContext context)
    {
        _context = context;
    }

    public void Update(PrefabSimWorld world)
    {
        if (_context.MatchOutcome != SplineMatchOutcome.Running)
        {
            return;
        }

        EntityHandle playerEntity = _context.PlayerEntity;
        if (!playerEntity.IsValid || !world.Player.TryGetRow(playerEntity, out _))
        {
            _context.SetMatchOutcome(SplineMatchOutcome.Lost);
            return;
        }

        if (world.BaseEnemy.Count <= 0)
        {
            _context.SetMatchOutcome(SplineMatchOutcome.Won);
        }
    }
}
