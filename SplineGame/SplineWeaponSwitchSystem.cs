using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;
using FixedMath;

namespace SplineGame;

public sealed class SplineWeaponSwitchSystem : IEcsSystem<PrefabSimWorld>
{
    private readonly SplineSimContext _context;

    public SplineWeaponSwitchSystem(SplineSimContext context)
    {
        _context = context;
    }

    public void Update(PrefabSimWorld world)
    {
        int weaponSwitchDelta = _context.WeaponSwitchDelta;
        if (weaponSwitchDelta == 0)
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

        ref SplineWeaponInventoryComponent inventory = ref playerShape.SplineWeaponInventory(playerRow);
        var weaponIds = new ResizableReadOnlyView<int>(world.VarHeap.Bytes, in inventory.WeaponIds);
        if (weaponIds.Count <= 0)
        {
            inventory.ActiveSlot = Fixed64.Zero;
            inventory.CooldownFramesRemaining = Fixed64.Zero;
            return;
        }

        int currentSlot = inventory.ActiveSlot.ToInt();
        if ((uint)currentSlot >= (uint)weaponIds.Count)
        {
            currentSlot = 0;
        }

        if (weaponSwitchDelta > 0)
        {
            currentSlot++;
            if (currentSlot >= weaponIds.Count)
            {
                currentSlot = 0;
            }
        }
        else
        {
            currentSlot--;
            if (currentSlot < 0)
            {
                currentSlot = weaponIds.Count - 1;
            }
        }

        inventory.ActiveSlot = Fixed64.FromInt(currentSlot);
        inventory.CooldownFramesRemaining = Fixed64.Zero;
    }
}
