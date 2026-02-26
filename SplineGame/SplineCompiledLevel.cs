using FixedMath;

namespace SplineGame;

public sealed class SplineCompiledLevel
{
    public SplineCompiledLevel(
        string levelRowId,
        string levelName,
        int levelId,
        int splineRowId,
        Point[] points)
    {
        LevelRowId = levelRowId;
        LevelName = levelName;
        LevelId = levelId;
        SplineRowId = splineRowId;
        Points = points;

        if (points.Length <= 0)
        {
            BoundsMinX = 0f;
            BoundsMaxX = 0f;
            BoundsMinY = 0f;
            BoundsMaxY = 0f;
            return;
        }

        float minX = points[0].X;
        float maxX = points[0].X;
        float minY = points[0].Y;
        float maxY = points[0].Y;

        for (int pointIndex = 1; pointIndex < points.Length; pointIndex++)
        {
            Point point = points[pointIndex];
            if (point.X < minX)
            {
                minX = point.X;
            }

            if (point.X > maxX)
            {
                maxX = point.X;
            }

            if (point.Y < minY)
            {
                minY = point.Y;
            }

            if (point.Y > maxY)
            {
                maxY = point.Y;
            }
        }

        BoundsMinX = minX;
        BoundsMaxX = maxX;
        BoundsMinY = minY;
        BoundsMaxY = maxY;
    }

    public string LevelRowId { get; }
    public string LevelName { get; }
    public int LevelId { get; }
    public int SplineRowId { get; }
    public Point[] Points { get; }
    public float BoundsMinX { get; }
    public float BoundsMaxX { get; }
    public float BoundsMinY { get; }
    public float BoundsMaxY { get; }

    public readonly struct Point
    {
        public Point(
            float x,
            float y,
            float tangentInX,
            float tangentInY,
            float tangentOutX,
            float tangentOutY)
        {
            X = x;
            Y = y;
            TangentInX = tangentInX;
            TangentInY = tangentInY;
            TangentOutX = tangentOutX;
            TangentOutY = tangentOutY;
        }

        public float X { get; }
        public float Y { get; }
        public float TangentInX { get; }
        public float TangentInY { get; }
        public float TangentOutX { get; }
        public float TangentOutY { get; }
    }
}
