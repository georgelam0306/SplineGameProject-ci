namespace SplineGame;

public static class Program
{
    public static void Main()
    {
        using var composition = new SplineAppComposition(
            curveSamplesPerSplineSegment: 22,
            playerCollisionRadius: 16f,
            triggerCooldownFrames: 20);
        composition.App.Run();
    }
}
