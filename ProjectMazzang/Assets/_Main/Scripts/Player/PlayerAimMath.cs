using UnityEngine;

public static class PlayerAimMath
{
    public static float CalculateLocalAngle(
        Vector2 worldDirection,
        bool facingRight)
    {
        Vector2 localDirection =
            facingRight
                ? worldDirection
                : new Vector2(
                    -worldDirection.x,
                    worldDirection.y);

        return Mathf.Atan2(
                   localDirection.y,
                   localDirection.x) *
               Mathf.Rad2Deg;
    }


    public static Vector2 GetWorldDirection(
        float localAngle,
        bool facingRight)
    {
        float radians =
            localAngle *
            Mathf.Deg2Rad;

        Vector2 direction =
            new(
                Mathf.Cos(radians),
                Mathf.Sin(radians));

        if (!facingRight)
        {
            direction.x *= -1f;
        }

        return direction.normalized;
    }


    public static Vector2 ResolveLimitedDirection(
        Vector2 direction,
        bool facingRight,
        float maxBodyAimAngle,
        float fallbackBodyAimAngle)
    {
        direction =
            direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector2.zero;

        if (direction == Vector2.zero)
        {
            return GetWorldDirection(
                fallbackBodyAimAngle,
                facingRight);
        }

        float localAngle =
            Mathf.Clamp(
                CalculateLocalAngle(
                    direction,
                    facingRight),
                -maxBodyAimAngle,
                maxBodyAimAngle);

        return GetWorldDirection(
            localAngle,
            facingRight);
    }
}
