using UnityEngine;

/// <summary>
/// 실제 투사체와 로컬 예측 표시가 공유하는 순수 탄도 계산입니다.
/// 호출자가 시뮬레이션 시점과 Transform 반영 책임을 가집니다.
/// </summary>
public static class ProjectileTrajectory
{
    public static int CalculateStepCount(
        Vector2 velocity,
        in ProjectileLaunchSettings settings,
        float deltaTime,
        float maxStepDistance,
        int maxSteps)
    {
        if (deltaTime <= 0f)
            return 1;

        Vector2 estimatedEndVelocity =
            velocity +
            Vector2.down *
            settings.GravityAcceleration *
            settings.GravityScale *
            deltaTime;

        float estimatedMaxSpeed =
            Mathf.Max(
                velocity.magnitude,
                estimatedEndVelocity.magnitude);

        float estimatedDistance =
            estimatedMaxSpeed *
            deltaTime;

        int stepCount =
            Mathf.CeilToInt(
                estimatedDistance /
                Mathf.Max(
                    0.005f,
                    maxStepDistance));

        return Mathf.Clamp(
            stepCount,
            1,
            Mathf.Max(
                1,
                maxSteps));
    }


    public static void Advance(
        ref Vector2 position,
        ref Vector2 velocity,
        in ProjectileLaunchSettings settings,
        float deltaTime,
        float maxStepDistance,
        int maxSteps)
    {
        int stepCount =
            CalculateStepCount(
                velocity,
                in settings,
                deltaTime,
                maxStepDistance,
                maxSteps);

        float stepDeltaTime =
            deltaTime /
            stepCount;

        for (int i = 0;
             i < stepCount;
             i++)
        {
            Step(
                ref position,
                ref velocity,
                in settings,
                stepDeltaTime);
        }
    }


    public static void Step(
        ref Vector2 position,
        ref Vector2 velocity,
        in ProjectileLaunchSettings settings,
        float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        velocity +=
            Vector2.down *
            settings.GravityAcceleration *
            settings.GravityScale *
            deltaTime;

        if (settings.LinearDrag > 0f)
        {
            velocity /=
                1f +
                settings.LinearDrag *
                deltaTime;
        }

        position +=
            velocity *
            deltaTime;
    }


    public static Vector2 NormalizeDirection(
        Vector2 direction)
    {
        return direction.sqrMagnitude >
               0.0001f
            ? direction.normalized
            : Vector2.zero;
    }


    public static Quaternion ResolveRotation(
        Vector2 velocity)
    {
        Vector2 direction =
            NormalizeDirection(
                velocity);

        if (direction == Vector2.zero)
            return Quaternion.identity;

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;

        return Quaternion.Euler(
            0f,
            0f,
            angle);
    }
}
