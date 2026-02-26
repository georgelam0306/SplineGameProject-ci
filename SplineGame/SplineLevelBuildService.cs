using DerpDocDatabase;
using DerpLib.DI;
using FixedMath;

namespace SplineGame;

public sealed class SplineLevelBuildService
{
    private readonly GameDatabase _database;
    private readonly SplineLevelCompiler _levelCompiler;
    private readonly int _curveSamplesPerSplineSegment;

    public SplineLevelBuildService(
        GameDatabase database,
        SplineLevelCompiler levelCompiler,
        [Arg("curveSamplesPerSplineSegment")] int curveSamplesPerSplineSegment)
    {
        _database = database;
        _levelCompiler = levelCompiler;
        _curveSamplesPerSplineSegment = curveSamplesPerSplineSegment;
    }

    public bool TryRebuild(
        string previousLevelRowId,
        out SplineCompiledLevel[] levels,
        out int targetLevelIndex,
        out string statusText,
        out Fixed64 referenceSplineLengthFixed,
        out float globalBoundsMinX,
        out float globalBoundsMinY,
        out float globalBoundsMaxX,
        out float globalBoundsMaxY)
    {
        targetLevelIndex = 0;
        referenceSplineLengthFixed = Fixed64.OneValue;
        globalBoundsMinX = 0f;
        globalBoundsMinY = 0f;
        globalBoundsMaxX = 0f;
        globalBoundsMaxY = 0f;

        if (!_levelCompiler.TryBuild(_database, out levels, out statusText))
        {
            return false;
        }

        if (levels.Length <= 0)
        {
            return true;
        }

        float referenceLength = SplineMath.ComputeApproxLength(levels[0].Points, _curveSamplesPerSplineSegment);
        if (!float.IsFinite(referenceLength) || referenceLength <= 0f)
        {
            referenceLength = 1f;
        }

        referenceSplineLengthFixed = Fixed64.FromFloat(referenceLength);

        globalBoundsMinX = levels[0].BoundsMinX;
        globalBoundsMinY = levels[0].BoundsMinY;
        globalBoundsMaxX = levels[0].BoundsMaxX;
        globalBoundsMaxY = levels[0].BoundsMaxY;

        for (int levelIndex = 1; levelIndex < levels.Length; levelIndex++)
        {
            ref readonly SplineCompiledLevel level = ref levels[levelIndex];
            globalBoundsMinX = MathF.Min(globalBoundsMinX, level.BoundsMinX);
            globalBoundsMinY = MathF.Min(globalBoundsMinY, level.BoundsMinY);
            globalBoundsMaxX = MathF.Max(globalBoundsMaxX, level.BoundsMaxX);
            globalBoundsMaxY = MathF.Max(globalBoundsMaxY, level.BoundsMaxY);
        }

        if (string.IsNullOrWhiteSpace(previousLevelRowId))
        {
            return true;
        }

        for (int levelIndex = 0; levelIndex < levels.Length; levelIndex++)
        {
            if (!string.Equals(levels[levelIndex].LevelRowId, previousLevelRowId, StringComparison.Ordinal))
            {
                continue;
            }

            targetLevelIndex = levelIndex;
            break;
        }

        return true;
    }
}
