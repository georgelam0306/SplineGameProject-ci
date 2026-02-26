using System.Numerics;
using DerpDocDatabase;
using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;
using FixedMath;

namespace SplineGame;

public sealed class SplineWeaponFireSystem : IEcsSystem<PrefabSimWorld>
{
    private const float MinimumDirectionMagnitudeSquared = 0.0001f;

    private readonly GameDatabase _database;
    private readonly SplineSimContext _context;

    public SplineWeaponFireSystem(GameDatabase database, SplineSimContext context)
    {
        _database = database;
        _context = context;
    }

    public void Update(PrefabSimWorld world)
    {
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
        if (inventory.CooldownFramesRemaining > Fixed64.Zero)
        {
            inventory.CooldownFramesRemaining -= Fixed64.OneValue;
            if (inventory.CooldownFramesRemaining < Fixed64.Zero)
            {
                inventory.CooldownFramesRemaining = Fixed64.Zero;
            }
        }

        if (!_context.FireHeld || inventory.CooldownFramesRemaining > Fixed64.Zero)
        {
            return;
        }

        var weaponIds = new ResizableReadOnlyView<int>(world.VarHeap.Bytes, in inventory.WeaponIds);
        if (weaponIds.Count <= 0)
        {
            return;
        }

        int activeSlot = inventory.ActiveSlot.ToInt();
        if ((uint)activeSlot >= (uint)weaponIds.Count)
        {
            activeSlot = 0;
            inventory.ActiveSlot = Fixed64.Zero;
        }

        int weaponId = weaponIds[activeSlot];
        if (!_database.Weapons.TryFindById(weaponId, out Weapons weapon))
        {
            return;
        }

        int projectileRowId = weapon.Projectile;
        if (projectileRowId < 0)
        {
            return;
        }

        Fixed64 playerParamT = playerShape.SplineTransform(playerRow).ParamT;
        if (_context.Points.Length < 2)
        {
            return;
        }

        Vector2 splineCentroid = SplineMath.ComputeCentroid(_context.Points);
        SplineMath.SamplePositionAndTangent(
            _context.Points,
            playerParamT.ToFloat(),
            out float playerX,
            out float playerY,
            out float tangentX,
            out float tangentY);

        Vector2 playerWorldPosition = new(playerX, playerY);
        Vector2 tangentDirection = new(tangentX, tangentY);
        Vector2 forwardDirection;
        if (!SplineMath.TryResolveInwardDirection(playerWorldPosition, tangentDirection, splineCentroid, out forwardDirection))
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
                Fixed64.FromFloat(playerX),
                Fixed64.FromFloat(playerY),
                Fixed64.FromFloat(forwardDirection.X),
                Fixed64.FromFloat(forwardDirection.Y),
                isEnemyProjectile: false))
        {
            return;
        }

        int cooldownFrames = weapon.FireRate;
        if (cooldownFrames < 1)
        {
            cooldownFrames = 1;
        }

        inventory.CooldownFramesRemaining = Fixed64.FromInt(cooldownFrames);
    }
}
