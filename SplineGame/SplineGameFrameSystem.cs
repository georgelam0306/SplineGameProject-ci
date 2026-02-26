using System.Numerics;
using DerpDocDatabase.Prefabs;
using DerpLib.Ecs;
using DerpLib.Rendering;
using DerpLib.Sdf;
using FixedMath;
using Silk.NET.Input;
using DerpEngine = DerpLib.Derp;

namespace SplineGame;

public sealed class SplineGameFrameSystem : IEcsSystem<SplineAppWorld>
{
    public SplineGameFrameSystem(SplineGameFrameContext context)
    {
        Context = context;
    }

    public SplineGameFrameContext Context { get; }

    public void Update(SplineAppWorld world)
    {
        if (!Context.IsRuntimeInitialized)
        {
            return;
        }

        DerpEngine.PollEvents();
        Context.Database.Update();

        if (DerpEngine.IsKeyPressed(Key.F5))
        {
            Context.Database.ReloadNow();
        }

        if (Context.TryConsumeReloadRequest())
        {
            RebuildRuntimeState();
        }

        Context.LevelLifecycleSystem.Update();

        PrefabSimWorld? simWorld = Context.LevelLifecycleSystem.SimWorld;
        if (simWorld != null)
        {
            if (Context.SimContext.MatchOutcome == SplineMatchOutcome.Running)
            {
                int moveAxis = 0;
                if (DerpEngine.IsKeyDown(Key.A) || DerpEngine.IsKeyDown(Key.Left))
                {
                    moveAxis--;
                }

                if (DerpEngine.IsKeyDown(Key.D) || DerpEngine.IsKeyDown(Key.Right))
                {
                    moveAxis++;
                }

                int weaponSwitchDelta = 0;
                if (DerpEngine.IsKeyPressed(Key.Q))
                {
                    weaponSwitchDelta--;
                }

                if (DerpEngine.IsKeyPressed(Key.E))
                {
                    weaponSwitchDelta++;
                }

                Context.SimContext.MoveAxis = moveAxis;
                Context.SimContext.WeaponSwitchDelta = weaponSwitchDelta;
                Context.SimContext.FireHeld = DerpEngine.IsKeyDown(Key.Space);
                Context.SimPipeline.RunFrame(simWorld);

                // Apply level transitions in the same frame if trigger systems requested one.
                Context.LevelLifecycleSystem.Update();
            }
            else
            {
                Context.SimContext.MoveAxis = 0;
                Context.SimContext.WeaponSwitchDelta = 0;
                Context.SimContext.FireHeld = false;

                if (DerpEngine.IsKeyPressed(Key.R) || DerpEngine.IsKeyPressed(Key.Space))
                {
                    Context.RequestRestart();
                }
            }
        }
        else
        {
            Context.SimContext.MoveAxis = 0;
            Context.SimContext.WeaponSwitchDelta = 0;
            Context.SimContext.FireHeld = false;
        }

        UpdateHudText();

        if (!DerpEngine.BeginDrawing())
        {
            return;
        }

        DrawFrame();
        DerpEngine.EndDrawing();
    }

    private void RebuildRuntimeState()
    {
        string previousLevelRowId = "";
        if (Context.LevelLifecycleSystem.TryGetCurrentLevel(out SplineCompiledLevel previousLevel))
        {
            previousLevelRowId = previousLevel.LevelRowId;
        }

        if (!Context.LevelBuildService.TryRebuild(
                previousLevelRowId,
                out SplineCompiledLevel[] compiledLevels,
                out int targetLevelIndex,
                out string statusText,
                out Fixed64 referenceSplineLength,
                out float globalBoundsMinX,
                out float globalBoundsMinY,
                out float globalBoundsMaxX,
                out float globalBoundsMaxY))
        {
            Context.StatusText = statusText;
            Context.LevelText = "Level: --";
            Context.EntityText = "Enemies: 0 Triggers: 0";
            Context.PrefabSpawnText = "Prefabs: --";
            Context.UiAssetCache?.Clear();
            Context.HasPrefabLoadError = false;

            Context.SimContext.SetLevelPoints(Array.Empty<SplineCompiledLevel.Point>());
            Context.SimContext.PlayerEntity = EntityHandle.Invalid;
            Context.LevelLifecycleSystem.SetCompiledLevels(Array.Empty<SplineCompiledLevel>(), 0);
            Context.LevelLifecycleSystem.SetPrefabBakedData(false, default, default);
            Context.RenderContext.ResetFrame();
            return;
        }

        Context.StatusText = statusText;
        Context.ReferenceSplineLengthFixed = referenceSplineLength;
        Context.GlobalBoundsMinX = globalBoundsMinX;
        Context.GlobalBoundsMinY = globalBoundsMinY;
        Context.GlobalBoundsMaxX = globalBoundsMaxX;
        Context.GlobalBoundsMaxY = globalBoundsMaxY;

        Context.SimContext.PlayerWorldSpeed = SplineGameFrameContext.PlayerMoveSpeedParam * Context.ReferenceSplineLengthFixed;
        Context.SimContext.EnemyWorldSpeedScale = Context.ReferenceSplineLengthFixed;

        Context.LevelLifecycleSystem.SetCompiledLevels(compiledLevels, targetLevelIndex);
        ReloadPrefabBakedData();
        Context.LevelLifecycleSystem.Update();
    }

    private void ReloadPrefabBakedData()
    {
        Context.HasPrefabLoadError = false;
        Context.PrefabSpawnText = "Prefabs: --";

        if (!Context.PrefabBakedDataLoader.TryLoad(
                out PrefabSimWorldUgcBakedAssets.BakedData simBaked,
                out PrefabRenderWorldUgcBakedAssets.BakedData renderBaked,
                out string loadError))
        {
            Context.LevelLifecycleSystem.SetPrefabBakedData(false, default, default);
            Context.HasPrefabLoadError = true;
            Context.PrefabSpawnText = "Prefabs: " + loadError;
            return;
        }

        Context.LevelLifecycleSystem.SetPrefabBakedData(true, simBaked, renderBaked);
    }

    private void UpdateHudText()
    {
        if (Context.LevelLifecycleSystem.TryGetCurrentLevel(out SplineCompiledLevel level))
        {
            Context.LevelText = "Level: " + level.LevelName + " (" + (Context.LevelLifecycleSystem.CurrentLevelIndex + 1) + "/" + Context.LevelLifecycleSystem.LevelCount + ")";
        }
        else
        {
            Context.LevelText = "Level: --";
        }

        int enemyCount = 0;
        int triggerCount = 0;
        int projectileCount = 0;
        PrefabSimWorld? simWorld = Context.LevelLifecycleSystem.SimWorld;
        if (simWorld != null)
        {
            enemyCount = simWorld.BaseEnemy.Count;
            triggerCount = simWorld.RoomTrigger.Count;
            projectileCount = simWorld.Projectile.Count;
        }

        Context.EntityText = "Enemies: " + enemyCount + " Triggers: " + triggerCount + " Projectiles: " + projectileCount;
        if (!Context.HasPrefabLoadError)
        {
            Context.PrefabSpawnText = "Prefabs: " + Context.LevelLifecycleSystem.PrefabSpawnSucceededCount + "/" + Context.LevelLifecycleSystem.PrefabSpawnRequestedCount;
        }
    }

    private void DrawFrame()
    {
        DerpEngine.SdfBuffer.Reset();

        int framebufferWidth = DerpEngine.GetFramebufferWidth();
        int framebufferHeight = DerpEngine.GetFramebufferHeight();
        float centerX = framebufferWidth * 0.5f;
        float centerY = framebufferHeight * 0.5f;
        float contentScale = DerpEngine.GetContentScale();

        if (Context.LevelLifecycleSystem.TryGetCurrentLevel(out SplineCompiledLevel currentLevel))
        {
            float splineWorldScale = ComputeGlobalWorldScale(framebufferWidth, framebufferHeight);
            float worldCenterX = (currentLevel.BoundsMinX + currentLevel.BoundsMaxX) * 0.5f;
            float worldCenterY = (currentLevel.BoundsMinY + currentLevel.BoundsMaxY) * 0.5f;
            float splineWorldOffsetX = centerX - (worldCenterX * splineWorldScale);
            float splineWorldOffsetY = centerY - (worldCenterY * splineWorldScale);

            DrawSpline(DerpEngine.SdfBuffer, currentLevel, splineWorldScale, splineWorldOffsetX, splineWorldOffsetY, contentScale);
            DrawLevelEntities(currentLevel, splineWorldScale, splineWorldOffsetX, splineWorldOffsetY);
        }

        float textScale = DerpEngine.GetContentScale();
        DerpEngine.DrawText(Context.UiFont, Context.StatusText, 12f * textScale, 12f * textScale, 20f * textScale);
        DerpEngine.DrawText(Context.UiFont, Context.LevelText, 12f * textScale, 38f * textScale, 18f * textScale);
        DerpEngine.DrawText(Context.UiFont, Context.EntityText, 12f * textScale, 60f * textScale, 18f * textScale);
        DerpEngine.DrawText(Context.UiFont, Context.PrefabSpawnText, 12f * textScale, 80f * textScale, 18f * textScale);
        DerpEngine.DrawText(Context.UiFont, SplineGameFrameContext.InputHelpText, 12f * textScale, 104f * textScale, 16f * textScale);
        DrawMatchOutcomeOverlay(framebufferWidth, framebufferHeight, textScale);

        DerpEngine.DispatchSdfToTexture();

        int screenWidth = DerpEngine.GetScreenWidth();
        int screenHeight = DerpEngine.GetScreenHeight();

        DerpEngine.BeginCamera2D(new Camera2D(Vector2.Zero, Vector2.Zero, 0f, 1f));

        Matrix4x4 backgroundTransform =
            Matrix4x4.CreateScale(screenWidth, screenHeight, 1f) *
            Matrix4x4.CreateTranslation(screenWidth * 0.5f, screenHeight * 0.5f, 0f);
        DerpEngine.DrawTextureTransform(Texture.White, backgroundTransform, 8, 11, 20, 255);

        Matrix4x4 sdfTransform =
            Matrix4x4.CreateScale(screenWidth, -screenHeight, 1f) *
            Matrix4x4.CreateTranslation(screenWidth * 0.5f, screenHeight * 0.5f, 0f);
        DerpEngine.DrawTextureTransform(DerpEngine.SdfOutputTexture, sdfTransform, 255, 255, 255, 255);

        DerpEngine.EndCamera2D();
    }

    private float ComputeGlobalWorldScale(int framebufferWidth, int framebufferHeight)
    {
        float boundsWidth = MathF.Max(1f, Context.GlobalBoundsMaxX - Context.GlobalBoundsMinX);
        float boundsHeight = MathF.Max(1f, Context.GlobalBoundsMaxY - Context.GlobalBoundsMinY);

        float scaleX = (framebufferWidth * SplineGameFrameContext.SplineFitPaddingFactor) / boundsWidth;
        float scaleY = (framebufferHeight * SplineGameFrameContext.SplineFitPaddingFactor) / boundsHeight;
        float scale = MathF.Min(scaleX, scaleY);
        if (!float.IsFinite(scale) || scale <= 0f)
        {
            return 1f;
        }

        return scale;
    }

    private void DrawSpline(
        SdfBuffer buffer,
        in SplineCompiledLevel level,
        float worldScale,
        float worldOffsetX,
        float worldOffsetY,
        float contentScale)
    {
        if (level.Points.Length < 2)
        {
            return;
        }

        int pointCount = level.Points.Length;
        int sampledPointCapacity = (pointCount * SplineGameFrameContext.CurveSegmentsPerSplineSegment) + 1;
        Context.EnsureSplinePolylineCapacity(sampledPointCapacity);
        Span<Vector2> sampledPoints = Context.SplinePolylinePoints.AsSpan(0, sampledPointCapacity);
        int sampledPointCount = 0;

        for (int segmentIndex = 0; segmentIndex < pointCount; segmentIndex++)
        {
            ref readonly SplineCompiledLevel.Point startPoint = ref level.Points[segmentIndex];
            ref readonly SplineCompiledLevel.Point endPoint = ref level.Points[(segmentIndex + 1) % pointCount];

            Vector2 p0 = new(startPoint.X, startPoint.Y);
            Vector2 p1 = new(startPoint.X + startPoint.TangentOutX, startPoint.Y + startPoint.TangentOutY);
            Vector2 p2 = new(endPoint.X + endPoint.TangentInX, endPoint.Y + endPoint.TangentInY);
            Vector2 p3 = new(endPoint.X, endPoint.Y);

            int firstCurvePointIndex = segmentIndex == 0 ? 0 : 1;
            for (int curvePointIndex = firstCurvePointIndex; curvePointIndex <= SplineGameFrameContext.CurveSegmentsPerSplineSegment; curvePointIndex++)
            {
                float t = curvePointIndex / (float)SplineGameFrameContext.CurveSegmentsPerSplineSegment;
                Vector2 currentWorldPoint = SplineMath.EvaluateCubicBezierPoint(p0, p1, p2, p3, t);
                sampledPoints[sampledPointCount] = new Vector2(
                    (currentWorldPoint.X * worldScale) + worldOffsetX,
                    (currentWorldPoint.Y * worldScale) + worldOffsetY);
                sampledPointCount++;
            }
        }

        buffer.AddPolyline(
            sampledPoints.Slice(0, sampledPointCount),
            2f * contentScale,
            0.26f,
            0.62f,
            1f,
            0.95f);
    }

    private void DrawLevelEntities(
        in SplineCompiledLevel level,
        float worldScale,
        float worldOffsetX,
        float worldOffsetY)
    {
        if (level.Points.Length < 2)
        {
            return;
        }

        PrefabSimWorld? simWorld = Context.LevelLifecycleSystem.SimWorld;
        PrefabRenderWorld? renderWorld = Context.LevelLifecycleSystem.RenderWorld;
        if (simWorld == null || renderWorld == null)
        {
            return;
        }

        Context.RenderContext.SetFrame(
            simWorld,
            level.Points,
            SplineMath.ComputeCentroid(level.Points),
            worldScale,
            worldOffsetX,
            worldOffsetY);
        Context.RenderPipeline.RunFrame(renderWorld);
    }

    private void DrawMatchOutcomeOverlay(int framebufferWidth, int framebufferHeight, float textScale)
    {
        SplineMatchOutcome outcome = Context.SimContext.MatchOutcome;
        if (outcome == SplineMatchOutcome.Running)
        {
            return;
        }

        string title;
        float red;
        float green;
        float blue;
        if (outcome == SplineMatchOutcome.Won)
        {
            title = "YOU WIN";
            red = 0.35f;
            green = 0.95f;
            blue = 0.45f;
        }
        else
        {
            title = "YOU LOSE";
            red = 1f;
            green = 0.35f;
            blue = 0.35f;
        }

        const string subtitle = "Press R or Space to Restart";
        float titleSize = 42f * textScale;
        float subtitleSize = 20f * textScale;
        float centerX = framebufferWidth * 0.5f;
        float centerY = framebufferHeight * 0.5f;

        Vector2 titleDimensions = Context.UiFont.MeasureText(title, titleSize);
        Vector2 subtitleDimensions = Context.UiFont.MeasureText(subtitle, subtitleSize);

        DerpEngine.DrawText(
            Context.UiFont,
            title,
            centerX - (titleDimensions.X * 0.5f),
            centerY - (24f * textScale),
            titleSize,
            0f,
            red,
            green,
            blue);

        DerpEngine.DrawText(
            Context.UiFont,
            subtitle,
            centerX - (subtitleDimensions.X * 0.5f),
            centerY + (20f * textScale),
            subtitleSize,
            0f,
            0.85f,
            0.86f,
            0.9f);
    }
}
