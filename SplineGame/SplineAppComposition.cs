using DerpLib.DI;
using static DerpLib.DI.DI;

namespace SplineGame;

[Composition]
internal partial class SplineAppComposition
{
    static void Setup() => DI.Setup()
        .Arg<int>("curveSamplesPerSplineSegment")
        .Arg<float>("playerCollisionRadius")
        .Arg<int>("triggerCooldownFrames")
        .Scope<SplineGameComposition>()
        .Bind<SplineGameHostApp>().As(Singleton).To<SplineGameHostApp>()
        .Root<SplineGameHostApp>("App");
}
