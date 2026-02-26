using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;

namespace SplineGame;

public static class SplineEntityDestroyHelper
{
    public static void QueueDestroySimAndRender(PrefabSimWorld simWorld, PrefabRenderWorld? renderWorld, EntityHandle simEntity)
    {
        if (!simEntity.IsValid)
        {
            return;
        }

        simWorld.Player.QueueDestroy(simEntity);
        simWorld.BaseEnemy.QueueDestroy(simEntity);
        simWorld.RoomTrigger.QueueDestroy(simEntity);
        simWorld.Projectile.QueueDestroy(simEntity);

        if (renderWorld == null)
        {
            return;
        }

        var renderShape = renderWorld.SimEntityLink_SplineEntityVisual;
        for (int renderRow = 0; renderRow < renderShape.Count; renderRow++)
        {
            ref readonly SimEntityLinkComponent link = ref renderShape.SimEntityLink(renderRow);
            if (link.SimEntity != simEntity)
            {
                continue;
            }

            EntityHandle renderEntity = renderShape.Entity(renderRow);
            renderShape.QueueDestroy(renderEntity);
        }
    }
}
