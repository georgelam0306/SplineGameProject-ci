using System.Globalization;
using DerpDocDatabase;

namespace SplineGame;

public sealed class SplineLevelCompiler
{
    public bool TryBuild(GameDatabase database, out SplineCompiledLevel[] levels, out string statusText)
    {
        levels = Array.Empty<SplineCompiledLevel>();
        statusText = "";

        int levelCount = database.GameLevels.Count;
        if (levelCount <= 0)
        {
            statusText = "GameLevels has no rows.";
            return false;
        }

        int[] firstSplineRowIdByLevelId = BuildFirstSplineRowIdByLevelId(database, levelCount);
        var compiledLevels = new List<SplineCompiledLevel>(levelCount);

        for (int levelId = 0; levelId < levelCount; levelId++)
        {
            int splineRowId = firstSplineRowIdByLevelId[levelId];
            if (splineRowId < 0)
            {
                continue;
            }

            ref readonly GameLevels gameLevelRow = ref database.GameLevels.FindById(levelId);
            if (!TryCompileLevel(
                    database,
                    levelId,
                    splineRowId,
                    gameLevelRow,
                    out SplineCompiledLevel compiledLevel,
                    out string levelError))
            {
                statusText = levelError;
                return false;
            }

            compiledLevels.Add(compiledLevel);
        }

        if (compiledLevels.Count <= 0)
        {
            statusText = "No GameLevels rows had any SplineGameLevel subtable rows.";
            return false;
        }

        levels = compiledLevels.ToArray();
        statusText = "Loaded " + levels.Length.ToString(CultureInfo.InvariantCulture) + " spline levels.";
        return true;
    }

    private static int[] BuildFirstSplineRowIdByLevelId(GameDatabase database, int levelCount)
    {
        var firstSplineRowIdByLevelId = new int[levelCount];
        Array.Fill(firstSplineRowIdByLevelId, -1);

        int splineRowCount = database.GameLevelsSplineGameLevel.Count;
        for (int splineRowId = 0; splineRowId < splineRowCount; splineRowId++)
        {
            ref readonly GameLevelsSplineGameLevel splineRow = ref database.GameLevelsSplineGameLevel.FindById(splineRowId);
            int levelId = splineRow.ParentRowId;
            if ((uint)levelId >= (uint)levelCount)
            {
                continue;
            }

            if (firstSplineRowIdByLevelId[levelId] < 0)
            {
                firstSplineRowIdByLevelId[levelId] = splineRowId;
            }
        }

        return firstSplineRowIdByLevelId;
    }

    private static bool TryCompileLevel(
        GameDatabase database,
        int levelId,
        int splineRowId,
        in GameLevels gameLevelRow,
        out SplineCompiledLevel compiledLevel,
        out string errorText)
    {
        compiledLevel = null!;
        errorText = "";

        if (!TryBuildPoints(database, splineRowId, out SplineCompiledLevel.Point[] points, out errorText))
        {
            return false;
        }

        string levelRowId = gameLevelRow.Id;
        string levelName = gameLevelRow.LevelName;
        if (string.IsNullOrWhiteSpace(levelName))
        {
            levelName = "Level " + (levelId + 1).ToString(CultureInfo.InvariantCulture);
        }

        compiledLevel = new SplineCompiledLevel(levelRowId, levelName, levelId, splineRowId, points);
        return true;
    }

    private static bool TryBuildPoints(
        GameDatabase database,
        int splineRowId,
        out SplineCompiledLevel.Point[] points,
        out string errorText)
    {
        points = Array.Empty<SplineCompiledLevel.Point>();
        errorText = "";

        var orderedPoints = new List<OrderedPointRow>(32);
        GameLevelsSplineGameLevelPointsTable.ParentScopedRange pointsRange =
            database.GameLevelsSplineGameLevel.FindByIdView(splineRowId).Points.All;
        var pointsEnumerator = pointsRange.GetEnumerator();
        while (pointsEnumerator.MoveNext())
        {
            ref readonly GameLevelsSplineGameLevelPoints pointRow = ref pointsEnumerator.Current;
            orderedPoints.Add(new OrderedPointRow(pointRow.Order, pointRow));
        }

        if (orderedPoints.Count < 2)
        {
            errorText = "SplineGameLevel points must contain at least 2 rows.";
            return false;
        }

        orderedPoints.Sort(static (left, right) => left.Order.CompareTo(right.Order));
        points = new SplineCompiledLevel.Point[orderedPoints.Count];
        for (int pointIndex = 0; pointIndex < orderedPoints.Count; pointIndex++)
        {
            GameLevelsSplineGameLevelPoints pointRow = orderedPoints[pointIndex].Row;
            points[pointIndex] = new SplineCompiledLevel.Point(
                pointRow.Position.X.ToFloat(),
                pointRow.Position.Y.ToFloat(),
                pointRow.TangentIn.X.ToFloat(),
                pointRow.TangentIn.Y.ToFloat(),
                pointRow.TangentOut.X.ToFloat(),
                pointRow.TangentOut.Y.ToFloat());
        }

        return true;
    }

    private readonly struct OrderedPointRow
    {
        public OrderedPointRow(int order, GameLevelsSplineGameLevelPoints row)
        {
            Order = order;
            Row = row;
        }

        public int Order { get; }
        public GameLevelsSplineGameLevelPoints Row { get; }
    }
}
