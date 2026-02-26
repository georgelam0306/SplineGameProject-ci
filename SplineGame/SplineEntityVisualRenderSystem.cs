using System.Numerics;
using DerpDocDatabase;
using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;
using DerpLib.Rendering;
using DerpLib.Sdf;
using FixedMath;
using DerpEngine = DerpLib.Derp;

namespace SplineGame;

public sealed class SplineEntityVisualRenderSystem : IEcsSystem<PrefabRenderWorld>
{
    private const float DefaultEntityUiScale = 0.1f;
    private const float EntityFallbackSize = 14f;
    private const float MinimumDirectionMagnitudeSquared = 0.0001f;
    private const float HealthBarWidth = 28f;
    private const float HealthBarHeight = 3f;
    private const float HealthBarYOffset = 24f;

    private readonly SplineRenderContext _context;

    public SplineEntityVisualRenderSystem(SplineRenderContext context)
    {
        _context = context;
    }

    public void Update(PrefabRenderWorld world)
    {
        PrefabSimWorld? simWorld = _context.SimWorld;
        if (simWorld == null)
        {
            return;
        }

        if (_context.LevelPoints.Length < 2)
        {
            return;
        }

        var renderShape = world.SimEntityLink_SplineEntityVisual;
        for (int renderRow = 0; renderRow < renderShape.Count; renderRow++)
        {
            ref readonly SimEntityLinkComponent link = ref renderShape.SimEntityLink(renderRow);
            ref readonly SplineEntityVisualComponent visual = ref renderShape.SplineEntityVisual(renderRow);

            if (!TryResolveSimPoseAndKind(simWorld, link.SimEntity, out EntityKind kind, out Vector2 worldPosition, out Vector2 facingDirection))
            {
                continue;
            }

            string uiAssetPath = "";
            if (_context.Database.Ui.TryFindById(visual.UiAsset, out Ui uiRow))
            {
                uiAssetPath = uiRow.RelativePath;
            }

            float uiScale = ResolveUiScale(visual.UiScale);
            (byte red, byte green, byte blue) = kind switch
            {
                EntityKind.Player => ((byte)60, (byte)230, (byte)90),
                EntityKind.Enemy => ((byte)242, (byte)96, (byte)96),
                EntityKind.Trigger => ((byte)255, (byte)188, (byte)78),
                EntityKind.Projectile => ((byte)110, (byte)186, (byte)255),
                _ => ((byte)200, (byte)200, (byte)200),
            };

            DrawMarkerAtWorldPosition(worldPosition, facingDirection, uiAssetPath, uiScale, red, green, blue);

            if (TryResolveHealthBar(simWorld, link.SimEntity, out float healthRatio))
            {
                DrawHealthBar(worldPosition, healthRatio);
            }
        }
    }

    private void DrawMarkerAtWorldPosition(
        in Vector2 worldPosition,
        in Vector2 facingDirection,
        string uiAssetPath,
        float uiScale,
        byte fallbackRed,
        byte fallbackGreen,
        byte fallbackBlue)
    {
        if (TryDrawUiMarker(uiAssetPath, uiScale, worldPosition, facingDirection))
        {
            return;
        }

        DrawFallbackMarker(worldPosition, fallbackRed, fallbackGreen, fallbackBlue);
    }

    private bool TryDrawUiMarker(
        string uiAssetPath,
        float uiScale,
        Vector2 worldPosition,
        Vector2 facingDirection)
    {
        float drawScale = _context.WorldScale * uiScale;

        if (_context.UiAssetCache == null ||
            !_context.UiAssetCache.TryGetVisual(uiAssetPath, drawScale, out SplineUiAssetCache.UiAssetVisual uiVisual))
        {
            return false;
        }

        float drawWidth = MathF.Max(1f, uiVisual.PrefabSize.X * drawScale);
        float drawHeight = MathF.Max(1f, uiVisual.PrefabSize.Y * drawScale);
        float screenX = (worldPosition.X * _context.WorldScale) + _context.WorldOffsetX;
        float screenY = (worldPosition.Y * _context.WorldScale) + _context.WorldOffsetY;
        Texture uiTexture = uiVisual.Texture;
        float inverseTextureWidth = uiTexture.Width > 0 ? 1f / uiTexture.Width : 0f;
        float inverseTextureHeight = uiTexture.Height > 0 ? 1f / uiTexture.Height : 0f;
        float uInset = MathF.Min(0.49f, inverseTextureWidth * 0.5f);
        float vInset = MathF.Min(0.49f, inverseTextureHeight * 0.5f);

        Vector2 normalizedFacingDirection = NormalizeDirection(facingDirection);
        float rotationRadians = MathF.Atan2(normalizedFacingDirection.X, -normalizedFacingDirection.Y);

        var markerCommand = SdfCommand.Image(
                new Vector2(screenX, screenY),
                new Vector2(drawWidth * 0.5f, drawHeight * 0.5f),
                new Vector4(uInset, vInset, 1f - uInset, 1f - vInset),
                new Vector4(1f, 1f, 1f, 1f),
                (uint)uiTexture.GetIndex())
            .WithRotation(rotationRadians);
        DerpEngine.SdfBuffer.Add(markerCommand);
        return true;
    }

    private void DrawFallbackMarker(
        Vector2 worldPosition,
        byte red,
        byte green,
        byte blue)
    {
        float screenX = (worldPosition.X * _context.WorldScale) + _context.WorldOffsetX;
        float screenY = (worldPosition.Y * _context.WorldScale) + _context.WorldOffsetY;

        float colorScale = 1f / 255f;
        var fallbackCommand = SdfCommand.Rect(
            new Vector2(screenX, screenY),
            new Vector2(EntityFallbackSize * 0.5f, EntityFallbackSize * 0.5f),
            new Vector4(
                red * colorScale,
                green * colorScale,
                blue * colorScale,
                225f * colorScale));
        DerpEngine.SdfBuffer.Add(fallbackCommand);
    }

    private bool TryResolveHealthBar(PrefabSimWorld simWorld, EntityHandle simEntity, out float healthRatio)
    {
        if (simWorld.Player.TryGetRow(simEntity, out int playerRow))
        {
            ref readonly SplineHealthComponent health = ref simWorld.Player.SplineHealth(playerRow);
            return TryComputeHealthBarRatio(in health, out healthRatio);
        }

        if (simWorld.BaseEnemy.TryGetRow(simEntity, out int enemyRow))
        {
            ref readonly SplineHealthComponent health = ref simWorld.BaseEnemy.SplineHealth(enemyRow);
            return TryComputeHealthBarRatio(in health, out healthRatio);
        }

        healthRatio = 1f;
        return false;
    }

    private static bool TryComputeHealthBarRatio(in SplineHealthComponent health, out float healthRatio)
    {
        if (health.DamageFlashFramesRemaining <= Fixed64.Zero)
        {
            healthRatio = 1f;
            return false;
        }

        float maxHealth = health.MaxHp.ToFloat();
        if (!float.IsFinite(maxHealth) || maxHealth <= 0f)
        {
            healthRatio = 1f;
            return false;
        }

        float currentHealth = health.Hp.ToFloat();
        if (!float.IsFinite(currentHealth))
        {
            currentHealth = 0f;
        }

        healthRatio = Math.Clamp(currentHealth / maxHealth, 0f, 1f);
        return true;
    }

    private void DrawHealthBar(Vector2 worldPosition, float healthRatio)
    {
        float contentScale = DerpEngine.GetContentScale();
        float screenX = (worldPosition.X * _context.WorldScale) + _context.WorldOffsetX;
        float screenY = (worldPosition.Y * _context.WorldScale) + _context.WorldOffsetY - (HealthBarYOffset * contentScale);

        float barWidth = HealthBarWidth * contentScale;
        float barHeight = HealthBarHeight * contentScale;
        float barHalfWidth = barWidth * 0.5f;
        float barHalfHeight = barHeight * 0.5f;

        var backgroundCommand = SdfCommand.Rect(
            new Vector2(screenX, screenY),
            new Vector2(barHalfWidth + (1.5f * contentScale), barHalfHeight + (1.5f * contentScale)),
            new Vector4(0.07f, 0.09f, 0.12f, 0.88f));
        DerpEngine.SdfBuffer.Add(backgroundCommand);

        float fillWidth = barWidth * healthRatio;
        if (fillWidth <= 0f)
        {
            return;
        }

        float fillHalfWidth = fillWidth * 0.5f;
        float fillCenterX = screenX - barHalfWidth + fillHalfWidth;
        float fillRed = 1f - healthRatio;
        float fillGreen = 0.2f + (0.8f * healthRatio);
        var fillCommand = SdfCommand.Rect(
            new Vector2(fillCenterX, screenY),
            new Vector2(fillHalfWidth, barHalfHeight),
            new Vector4(fillRed, fillGreen, 0.2f, 0.95f));
        DerpEngine.SdfBuffer.Add(fillCommand);
    }

    private static Vector2 NormalizeDirection(Vector2 direction)
    {
        float directionLengthSquared = direction.LengthSquared();
        if (directionLengthSquared > MinimumDirectionMagnitudeSquared)
        {
            return direction / MathF.Sqrt(directionLengthSquared);
        }

        return new Vector2(0f, -1f);
    }

    private static float ResolveUiScale(float configuredUiScale)
    {
        if (float.IsFinite(configuredUiScale) && configuredUiScale > 0f)
        {
            return configuredUiScale;
        }

        return DefaultEntityUiScale;
    }

    private bool TryResolveSimPoseAndKind(
        PrefabSimWorld simWorld,
        EntityHandle simEntity,
        out EntityKind kind,
        out Vector2 worldPosition,
        out Vector2 facingDirection)
    {
        kind = EntityKind.Unknown;
        worldPosition = default;
        facingDirection = default;

        if (simWorld.Player.TryGetRow(simEntity, out int playerRow))
        {
            if (!TryResolveSplinePose(simWorld.Player.SplineTransform(playerRow).ParamT, out worldPosition, out facingDirection))
            {
                return false;
            }

            kind = EntityKind.Player;
            return true;
        }

        if (simWorld.BaseEnemy.TryGetRow(simEntity, out int enemyRow))
        {
            if (!TryResolveSplinePose(simWorld.BaseEnemy.SplineTransform(enemyRow).ParamT, out worldPosition, out facingDirection))
            {
                return false;
            }

            kind = EntityKind.Enemy;
            return true;
        }

        if (simWorld.RoomTrigger.TryGetRow(simEntity, out int triggerRow))
        {
            if (!TryResolveSplinePose(simWorld.RoomTrigger.SplineTransform(triggerRow).ParamT, out worldPosition, out facingDirection))
            {
                return false;
            }

            kind = EntityKind.Trigger;
            return true;
        }

        if (simWorld.Projectile.TryGetRow(simEntity, out int projectileRow))
        {
            ref readonly SplineProjectileComponent projectile = ref simWorld.Projectile.SplineProjectile(projectileRow);
            worldPosition = new Vector2(projectile.PositionX.ToFloat(), projectile.PositionY.ToFloat());
            facingDirection = new Vector2(projectile.VelocityX.ToFloat(), projectile.VelocityY.ToFloat());
            kind = EntityKind.Projectile;
            return true;
        }

        return false;
    }

    private bool TryResolveSplinePose(Fixed64 paramT, out Vector2 worldPosition, out Vector2 facingDirection)
    {
        SplineMath.SamplePositionAndTangent(
            _context.LevelPoints,
            paramT.ToFloat(),
            out float worldX,
            out float worldY,
            out float tangentX,
            out float tangentY);

        worldPosition = new Vector2(worldX, worldY);
        Vector2 tangentDirection = new(tangentX, tangentY);
        if (!SplineMath.TryResolveInwardDirection(worldPosition, tangentDirection, _context.SplineCentroid, out facingDirection))
        {
            facingDirection = new Vector2(0f, -1f);
        }

        return true;
    }

    private enum EntityKind
    {
        Unknown = 0,
        Player = 1,
        Enemy = 2,
        Trigger = 3,
        Projectile = 4,
    }
}
