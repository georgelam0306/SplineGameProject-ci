using DerpDocDatabase;
using DerpDocDatabase.Prefabs;
using DerpLib.DI;
using DerpLib.Ecs;
using static DerpLib.DI.DI;

namespace SplineGame;

[Composition]
internal partial class SplineGameComposition
{
    static void Setup() => DI.Setup()
        .Arg<int>("curveSamplesPerSplineSegment")
        .Arg<float>("playerCollisionRadius")
        .Arg<int>("triggerCooldownFrames")
        .Bind<GameDatabase>().As(Singleton).To<GameDatabase>()
        .Bind<SplineLevelCompiler>().As(Singleton).To<SplineLevelCompiler>()
        .Bind<SplineLevelBuildService>().As(Singleton).To<SplineLevelBuildService>()
        .Bind<SplinePrefabBakedDataLoader>().As(Singleton).To<SplinePrefabBakedDataLoader>()
        .Bind<SplineSimContext>().As(Singleton).To<SplineSimContext>()
        .Bind<SplineLevelSpawnContext>().As(Singleton).To<SplineLevelSpawnContext>()
        .Bind<SplineRenderContext>().As(Singleton).To<SplineRenderContext>()
        .Bind<SplineUiAssetPreloadContext>().As(Singleton).To<SplineUiAssetPreloadContext>()
        .BindAll<IEcsSystem<PrefabSimWorld>>("Spawn").As(Singleton)
            .Add<SplineLevelSpawnSystem>()
        .BindAll<IEcsSystem<PrefabSimWorld>>("Sim").As(Singleton)
            .Add<SplinePlayerMoveSystem>()
            .Add<SplineWeaponSwitchSystem>()
            .Add<SplineWeaponFireSystem>()
            .Add<SplineEnemyFireSystem>()
            .Add<SplineProjectileSpawnSystem>()
            .Add<SplineProjectileMoveSystem>()
            .Add<SplineProjectileHitSystem>()
            .Add<SplineHealthFeedbackSystem>()
            .Add<SplineMatchOutcomeSystem>()
            .Add<SplineTriggerTransitionSystem>()
        .BindAll<IEcsSystem<PrefabRenderWorld>>("Render").As(Singleton)
            .Add<SplineEntityVisualRenderSystem>()
        .BindAll<IEcsSystem<PrefabRenderWorld>>("UiPreload").As(Singleton)
            .Add<SplineUiAssetPreloadSystem>()
        .Bind<EcsSystemPipeline<PrefabSimWorld>>("Spawn").As(Singleton).To<EcsSystemPipeline<PrefabSimWorld>>()
        .Bind<EcsSystemPipeline<PrefabSimWorld>>("Sim").As(Singleton).To<EcsSystemPipeline<PrefabSimWorld>>()
        .Bind<EcsSystemPipeline<PrefabRenderWorld>>("Render").As(Singleton).To<EcsSystemPipeline<PrefabRenderWorld>>()
        .Bind<EcsSystemPipeline<PrefabRenderWorld>>("UiPreload").As(Singleton).To<EcsSystemPipeline<PrefabRenderWorld>>()
        .Bind<SplineLevelLifecycleSystem>().As(Singleton).To<SplineLevelLifecycleSystem>()
        .Bind<SplineGameFrameContext>().As(Singleton).To<SplineGameFrameContext>()
        .Bind<SplineAppWorld>().As(Singleton).To<SplineAppWorld>()
        .BindAll<IEcsSystem<SplineAppWorld>>("Frame").As(Singleton)
            .Add<SplineGameFrameSystem>()
        .Bind<EcsSystemPipeline<SplineAppWorld>>("Frame").As(Singleton).To<EcsSystemPipeline<SplineAppWorld>>()
        .Bind<SplineGameApp>().As(Singleton).To<SplineGameApp>()
        .Root<SplineGameApp>("App");
}
