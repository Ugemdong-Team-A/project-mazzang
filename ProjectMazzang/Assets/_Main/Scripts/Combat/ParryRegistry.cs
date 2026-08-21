using System.Collections.Generic;
using UnityEngine;

public static class ParryRegistry
{
    private const float MinimumIncomingDot = -0.05f;
    private static readonly List<IParryVolume> Volumes = new();

    public static void Register(IParryVolume volume)
    {
        if (volume != null && !Volumes.Contains(volume))
            Volumes.Add(volume);
    }

    public static void Unregister(IParryVolume volume)
    {
        if (volume != null)
            Volumes.Remove(volume);
    }

    public static bool TryParry(
        IParryable target,
        Vector2 segmentStart,
        Vector2 segmentEnd)
    {
        if (target == null || target.ParryVelocity.sqrMagnitude <= 0.0001f)
            return false;

        Vector2 incoming = target.ParryVelocity.normalized;

        for (int i = Volumes.Count - 1; i >= 0; i--)
        {
            IParryVolume volume = Volumes[i];
            if (volume == null)
            {
                Volumes.RemoveAt(i);
                continue;
            }

            if (!volume.IsParryActive ||
                volume.ParryOwner == null ||
                target.ParrySource == volume.ParryOwner)
            {
                continue;
            }

            Vector2 facing = NormalizeOrRight(volume.ParryDirection);
            if (Vector2.Dot(incoming, facing) > MinimumIncomingDot)
                continue;

            Vector2 closest = ClosestPointOnSegment(
                segmentStart,
                segmentEnd,
                volume.ParryOrigin);

            Vector2 offset = closest - volume.ParryOrigin;
            if (offset.sqrMagnitude > volume.ParryRadius * volume.ParryRadius)
                continue;

            if (offset.sqrMagnitude > 0.0001f &&
                Vector2.Angle(facing, offset) > volume.ParryHalfAngle)
            {
                continue;
            }

            Vector2 reflected = Vector2.Reflect(incoming, facing);
            Vector2 outgoing = Vector2.Lerp(
                reflected,
                facing,
                Mathf.Clamp01(volume.ParryAimInfluence)).normalized;

            ParryHit hit = new(
                volume.ParryOwner,
                closest,
                outgoing,
                volume.ParrySpeedMultiplier);

            if (!target.TryParry(in hit))
                continue;

            volume.OnParrySuccess(closest);
            return true;
        }

        return false;
    }

    private static Vector2 ClosestPointOnSegment(
        Vector2 start,
        Vector2 end,
        Vector2 point)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= 0.0001f)
            return start;

        float t = Mathf.Clamp01(
            Vector2.Dot(point - start, segment) / lengthSquared);
        return start + segment * t;
    }

    private static Vector2 NormalizeOrRight(Vector2 direction)
    {
        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.right;
    }
}
