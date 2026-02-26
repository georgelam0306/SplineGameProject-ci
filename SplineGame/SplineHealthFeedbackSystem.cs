using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;
using FixedMath;

namespace SplineGame;

public sealed class SplineHealthFeedbackSystem : IEcsSystem<PrefabSimWorld>
{
    public void Update(PrefabSimWorld world)
    {
        var playerShape = world.Player;
        for (int playerRow = 0; playerRow < playerShape.Count; playerRow++)
        {
            ref SplineHealthComponent health = ref playerShape.SplineHealth(playerRow);
            TickDamageFlash(ref health);
        }

        var enemyShape = world.BaseEnemy;
        for (int enemyRow = 0; enemyRow < enemyShape.Count; enemyRow++)
        {
            ref SplineHealthComponent health = ref enemyShape.SplineHealth(enemyRow);
            TickDamageFlash(ref health);
        }
    }

    private static void TickDamageFlash(ref SplineHealthComponent health)
    {
        if (health.DamageFlashFramesRemaining <= Fixed64.Zero)
        {
            return;
        }

        health.DamageFlashFramesRemaining -= Fixed64.OneValue;
        if (health.DamageFlashFramesRemaining < Fixed64.Zero)
        {
            health.DamageFlashFramesRemaining = Fixed64.Zero;
        }
    }
}
