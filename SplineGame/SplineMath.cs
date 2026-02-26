using System.Numerics;
using FixedMath;

namespace SplineGame;

public static class SplineMath
{
    private const float MinimumTangentMagnitude = 0.0001f;
    private const float MinimumDirectionMagnitudeSquared = 0.0001f;
    private const float MinimumSpeedPerParam = 0.0001f;

    public static Fixed64 Wrap01(Fixed64 value)
    {
        while (value < Fixed64.Zero)
        {
            value += Fixed64.OneValue;
        }

        while (value >= Fixed64.OneValue)
        {
            value -= Fixed64.OneValue;
        }

        return value;
    }

    public static void SamplePositionAndTangent(
        ReadOnlySpan<SplineCompiledLevel.Point> points,
        float paramT,
        out float x,
        out float y,
        out float tangentX,
        out float tangentY)
    {
        if (points.Length <= 0)
        {
            x = 0f;
            y = 0f;
            tangentX = 1f;
            tangentY = 0f;
            return;
        }

        if (points.Length == 1)
        {
            x = points[0].X;
            y = points[0].Y;
            tangentX = 1f;
            tangentY = 0f;
            return;
        }

        float wrappedT = paramT - MathF.Floor(paramT);
        if (wrappedT < 0f)
        {
            wrappedT += 1f;
        }

        int segmentCount = points.Length;
        float segmentT = wrappedT * segmentCount;
        int segmentIndex = (int)segmentT;
        if (segmentIndex >= segmentCount)
        {
            segmentIndex = segmentCount - 1;
        }

        int nextSegmentIndex = segmentIndex + 1;
        if (nextSegmentIndex >= segmentCount)
        {
            nextSegmentIndex = 0;
        }

        float localT = segmentT - segmentIndex;
        ref readonly SplineCompiledLevel.Point startPoint = ref points[segmentIndex];
        ref readonly SplineCompiledLevel.Point endPoint = ref points[nextSegmentIndex];

        float p0x = startPoint.X;
        float p0y = startPoint.Y;
        float p1x = startPoint.X + startPoint.TangentOutX;
        float p1y = startPoint.Y + startPoint.TangentOutY;
        float p2x = endPoint.X + endPoint.TangentInX;
        float p2y = endPoint.Y + endPoint.TangentInY;
        float p3x = endPoint.X;
        float p3y = endPoint.Y;

        x = Cubic(p0x, p1x, p2x, p3x, localT);
        y = Cubic(p0y, p1y, p2y, p3y, localT);
        tangentX = CubicDerivative(p0x, p1x, p2x, p3x, localT);
        tangentY = CubicDerivative(p0y, p1y, p2y, p3y, localT);

        float tangentMagnitude = MathF.Sqrt((tangentX * tangentX) + (tangentY * tangentY));
        if (tangentMagnitude > MinimumTangentMagnitude)
        {
            tangentX /= tangentMagnitude;
            tangentY /= tangentMagnitude;
            return;
        }

        tangentX = 1f;
        tangentY = 0f;
    }

    public static void SamplePositionAndTangentAndSpeedPerParam(
        ReadOnlySpan<SplineCompiledLevel.Point> points,
        float paramT,
        out float x,
        out float y,
        out float tangentX,
        out float tangentY,
        out float speedPerParam)
    {
        if (points.Length <= 0)
        {
            x = 0f;
            y = 0f;
            tangentX = 1f;
            tangentY = 0f;
            speedPerParam = 1f;
            return;
        }

        if (points.Length == 1)
        {
            x = points[0].X;
            y = points[0].Y;
            tangentX = 1f;
            tangentY = 0f;
            speedPerParam = 1f;
            return;
        }

        float wrappedT = paramT - MathF.Floor(paramT);
        if (wrappedT < 0f)
        {
            wrappedT += 1f;
        }

        int segmentCount = points.Length;
        float segmentT = wrappedT * segmentCount;
        int segmentIndex = (int)segmentT;
        if (segmentIndex >= segmentCount)
        {
            segmentIndex = segmentCount - 1;
        }

        int nextSegmentIndex = segmentIndex + 1;
        if (nextSegmentIndex >= segmentCount)
        {
            nextSegmentIndex = 0;
        }

        float localT = segmentT - segmentIndex;
        ref readonly SplineCompiledLevel.Point startPoint = ref points[segmentIndex];
        ref readonly SplineCompiledLevel.Point endPoint = ref points[nextSegmentIndex];

        float p0x = startPoint.X;
        float p0y = startPoint.Y;
        float p1x = startPoint.X + startPoint.TangentOutX;
        float p1y = startPoint.Y + startPoint.TangentOutY;
        float p2x = endPoint.X + endPoint.TangentInX;
        float p2y = endPoint.Y + endPoint.TangentInY;
        float p3x = endPoint.X;
        float p3y = endPoint.Y;

        x = Cubic(p0x, p1x, p2x, p3x, localT);
        y = Cubic(p0y, p1y, p2y, p3y, localT);

        // Derivative is expressed in local [0..1] segment space.
        float derivativeLocalX = CubicDerivative(p0x, p1x, p2x, p3x, localT);
        float derivativeLocalY = CubicDerivative(p0y, p1y, p2y, p3y, localT);
        float derivativeParamX = derivativeLocalX * segmentCount;
        float derivativeParamY = derivativeLocalY * segmentCount;

        float derivativeParamMagnitude = MathF.Sqrt((derivativeParamX * derivativeParamX) + (derivativeParamY * derivativeParamY));
        if (derivativeParamMagnitude > MinimumTangentMagnitude)
        {
            tangentX = derivativeParamX / derivativeParamMagnitude;
            tangentY = derivativeParamY / derivativeParamMagnitude;
            speedPerParam = derivativeParamMagnitude;
            return;
        }

        tangentX = 1f;
        tangentY = 0f;
        speedPerParam = 1f;
    }

    public static Fixed64 AdvanceParamByWorldDistance(
        ReadOnlySpan<SplineCompiledLevel.Point> points,
        Fixed64 currentParamT,
        Fixed64 deltaWorldDistance)
    {
        float speedPerParam;
        SamplePositionAndTangentAndSpeedPerParam(
            points,
            currentParamT.ToFloat(),
            out _,
            out _,
            out _,
            out _,
            out speedPerParam);

        if (!float.IsFinite(speedPerParam) || speedPerParam < MinimumSpeedPerParam)
        {
            return currentParamT;
        }

        float deltaParam = deltaWorldDistance.ToFloat() / speedPerParam;
        return Wrap01(currentParamT + Fixed64.FromFloat(deltaParam));
    }

    public static float ComputeApproxLength(ReadOnlySpan<SplineCompiledLevel.Point> points, int samplesPerSplineSegment)
    {
        if (points.Length < 2)
        {
            return 0f;
        }

        int sampleCount = samplesPerSplineSegment;
        if (sampleCount < 1)
        {
            sampleCount = 1;
        }

        float length = 0f;
        int pointCount = points.Length;
        Vector2 previousPosition = default;
        bool hasPreviousPosition = false;

        for (int segmentIndex = 0; segmentIndex < pointCount; segmentIndex++)
        {
            ref readonly SplineCompiledLevel.Point startPoint = ref points[segmentIndex];
            ref readonly SplineCompiledLevel.Point endPoint = ref points[(segmentIndex + 1) % pointCount];

            Vector2 p0 = new(startPoint.X, startPoint.Y);
            Vector2 p1 = new(startPoint.X + startPoint.TangentOutX, startPoint.Y + startPoint.TangentOutY);
            Vector2 p2 = new(endPoint.X + endPoint.TangentInX, endPoint.Y + endPoint.TangentInY);
            Vector2 p3 = new(endPoint.X, endPoint.Y);

            for (int curvePointIndex = 0; curvePointIndex <= sampleCount; curvePointIndex++)
            {
                float t = curvePointIndex / (float)sampleCount;
                Vector2 currentPosition = EvaluateCubicBezierPoint(p0, p1, p2, p3, t);
                if (hasPreviousPosition)
                {
                    length += Vector2.Distance(previousPosition, currentPosition);
                }

                previousPosition = currentPosition;
                hasPreviousPosition = true;
            }
        }

        return length;
    }

    public static Vector2 ComputeCentroid(ReadOnlySpan<SplineCompiledLevel.Point> points)
    {
        if (points.Length <= 0)
        {
            return Vector2.Zero;
        }

        Vector2 sum = Vector2.Zero;
        for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
        {
            ref readonly SplineCompiledLevel.Point point = ref points[pointIndex];
            sum += new Vector2(point.X, point.Y);
        }

        return sum / points.Length;
    }

    public static bool TryResolveInwardDirection(
        in Vector2 worldPosition,
        in Vector2 tangentDirection,
        in Vector2 splineCentroid,
        out Vector2 inwardDirection)
    {
        float tangentLengthSquared = tangentDirection.LengthSquared();
        if (tangentLengthSquared <= MinimumDirectionMagnitudeSquared)
        {
            Vector2 centroidDirection = splineCentroid - worldPosition;
            float centroidLengthSquared = centroidDirection.LengthSquared();
            if (centroidLengthSquared <= MinimumDirectionMagnitudeSquared)
            {
                inwardDirection = default;
                return false;
            }

            inwardDirection = centroidDirection / MathF.Sqrt(centroidLengthSquared);
            return true;
        }

        Vector2 tangentUnit = tangentDirection / MathF.Sqrt(tangentLengthSquared);
        Vector2 leftNormal = new(-tangentUnit.Y, tangentUnit.X);
        Vector2 rightNormal = new(tangentUnit.Y, -tangentUnit.X);
        Vector2 centroidVector = splineCentroid - worldPosition;
        float centroidVectorLengthSquared = centroidVector.LengthSquared();
        if (centroidVectorLengthSquared <= MinimumDirectionMagnitudeSquared)
        {
            inwardDirection = leftNormal;
            return true;
        }

        float leftDot = Vector2.Dot(leftNormal, centroidVector);
        float rightDot = Vector2.Dot(rightNormal, centroidVector);
        inwardDirection = leftDot >= rightDot ? leftNormal : rightNormal;
        return true;
    }

    public static Vector2 EvaluateCubicBezierPoint(
        in Vector2 p0,
        in Vector2 p1,
        in Vector2 p2,
        in Vector2 p3,
        float t)
    {
        float clampedT = Math.Clamp(t, 0f, 1f);
        float oneMinusT = 1f - clampedT;
        float oneMinusTSquared = oneMinusT * oneMinusT;
        float oneMinusTCubed = oneMinusTSquared * oneMinusT;
        float tSquared = clampedT * clampedT;
        float tCubed = tSquared * clampedT;

        return (oneMinusTCubed * p0) +
               (3f * oneMinusTSquared * clampedT * p1) +
               (3f * oneMinusT * tSquared * p2) +
               (tCubed * p3);
    }

    private static float Cubic(float p0, float p1, float p2, float p3, float t)
    {
        float inverseT = 1f - t;
        float inverseTSquared = inverseT * inverseT;
        float inverseTCubed = inverseTSquared * inverseT;
        float tSquared = t * t;
        float tCubed = tSquared * t;
        return (inverseTCubed * p0) + (3f * inverseTSquared * t * p1) + (3f * inverseT * tSquared * p2) + (tCubed * p3);
    }

    private static float CubicDerivative(float p0, float p1, float p2, float p3, float t)
    {
        float inverseT = 1f - t;
        float a = 3f * inverseT * inverseT * (p1 - p0);
        float b = 6f * inverseT * t * (p2 - p1);
        float c = 3f * t * t * (p3 - p2);
        return a + b + c;
    }
}
