using System;
using Fusion;
using UnityEngine;

public class ProjectileMovement : MonoBehaviour
{
    private float _lifetime;

    private float _maxDistance;

    private float _gravityScale = 1f;

    private float _gravityAcceleration = 9.81f;

    private float _linearDrag = 0f;

    private bool _alignRotationToVelocity = true;


    private bool _initialized = false;

    private bool _expired = false;

    private float _lifeTimer;

    [SerializeField]
    private Vector2 _velocity;

    public event Action OnExpired;

    private void Update()
    {
        SimulateBallisticMovement(Time.deltaTime);
    }

    public void Initialize(
        WeaponFirePose firePose,
        ProjectileLaunchSettings settings,
        Vector2? optionalDir = null)
    {
        if (_initialized) return;

        _lifetime = settings.Lifetime;
        _gravityScale = settings.GravityScale;
        _gravityAcceleration = settings.GravityAcceleration;
        _linearDrag = settings.LinearDrag;
        _alignRotationToVelocity = settings.AlignRotationToVelocity;

        _initialized = true;

        // 방향과 위치 초기화는 Movement 담당이 아닐 것 같음.
        // 스폰 메서드에 옵션이 있기도 하고, 샷건같이, 발사 방향과 실제 투사체의 위치가 다른 경우도 있어서
        /*transform.position = firePose.Origin;

        Vector2 direction = firePose.Direction;
        if (direction == Vector2.zero)
            direction = transform.right;
        transform.right = direction;*/

        Debug.Log(optionalDir);

        _velocity = (optionalDir.HasValue ?
            optionalDir.Value :
            firePose.Direction) * settings.Speed;
    }

    public void SimulateBallisticMovement(float deltaTime)
    {
        if (!_initialized)
        {
            Debug.Log("투사체가 초기화되지 않음");
            return;
        }

        if (!_expired &&
            _lifeTimer >= _lifetime)
        {
            Debug.Log("투사체 수명이 다됨!");
            _expired = true;
            OnExpired?.Invoke();

            return;
        }

        _lifeTimer += deltaTime;


        Vector2 gravity =
            Vector2.down *
            _gravityAcceleration *
            _gravityScale;

        ApplyBallistics(
            gravity,
            deltaTime);

        SimulateMovementStep(deltaTime);

        /*Vector2 estimatedEndVelocity =
            _velocity +
            gravity *
            deltaTime;

        float estimatedMaxSpeed =
            Mathf.Max(
                _velocity.magnitude,
                estimatedEndVelocity.magnitude);

        float estimatedDistance =
            estimatedMaxSpeed *
            deltaTime;

        int stepCount =
            CalculateStepCount(
                estimatedDistance);

        float stepDeltaTime =
            deltaTime /
            stepCount;

        for (int i = 0;
             i < stepCount;
             i++)
        {
            ApplyBallistics(
                gravity,
                stepDeltaTime);

            if (!SimulateMovementStep(
                    stepDeltaTime))
            {
                return;
            }
        }*/

        ApplyRotationFromVelocity();
    }

    private void ApplyBallistics(
        Vector2 gravity,
        float deltaTime)
    {
        _velocity +=
            gravity *
            deltaTime;

        if (_linearDrag <= 0f)
            return;

        _velocity /=
            1f +
            _linearDrag *
            deltaTime;
    }


    private bool SimulateMovementStep(
        float deltaTime)
    {
        Vector2 displacement =
            _velocity *
            deltaTime;

        float distance =
            displacement.magnitude;

        if (distance <=
            0.0001f)
        {
            return true;
        }

        Vector2 direction =
            displacement /
            distance;

        Vector2 start =
            transform.position;

        /*if (TryFindCollision(
                start,
                direction,
                distance,
                out RaycastHit2D hit))
        {
            transform.position =
                start +
                direction *
                hit.distance;

            ApplyRotationFromVelocity();

            *//*OnImpact(
                hit);*//*

            return false;
        }*/

        transform.position =
            start +
            displacement;

        return true;
    }


    private int CalculateStepCount(
        float estimatedDistance)
    {
        float stepDistance =
            Mathf.Max(
                0.005f,
                _maxDistance);

        int stepCount =
            Mathf.CeilToInt(
                estimatedDistance /
                stepDistance);

        return Mathf.Clamp(
            stepCount,
            1,
            Mathf.Max(
                1,
                /*_maxStepsPerFrame*/16));
    }


    /*private bool TryFindCollision(
        Vector2 start,
        Vector2 direction,
        float distance,
        out RaycastHit2D nearestHit)
    {
        RaycastHit2D[] hits =
            Physics2D.CircleCastAll(
                start,
                _collisionRadius,
                direction,
                distance,
                _collisionMask);

        bool found =
            false;

        nearestHit =
            default;

        float nearestDistance =
            float.MaxValue;

        for (int i = 0;
             i < hits.Length;
             i++)
        {
            RaycastHit2D hit =
                hits[i];

            Collider2D candidate =
                hit.collider;

            if (candidate == null)
                continue;

            if (ShouldIgnoreCollider(
                    candidate))
            {
                continue;
            }

            if (hit.distance >=
                nearestDistance)
            {
                continue;
            }

            nearestDistance =
                hit.distance;

            nearestHit =
                hit;

            found =
                true;
        }

        return found;
    }*/

    protected virtual bool ShouldIgnoreCollider(
        Collider2D candidate)
    {
        if (candidate.transform ==
            transform)
        {
            return true;
        }

        if (candidate.transform.IsChildOf(
                transform))
        {
            return true;
        }

        /*NetworkObject targetObject =
            candidate.GetComponentInParent<
                NetworkObject>();

        if (targetObject == null)
            return false;

        if (targetObject ==
            Object)
        {
            return true;
        }

        if (Source != null &&
    targetObject == Source)
        {
            return true;
        }*/

        return false;
    }


    /*protected virtual void OnImpact(
        RaycastHit2D hit)
    {
        TryApplyDamage(
            hit.collider);

        LastImpactPosition =
            transform.position;

        HasImpacted =
            true;

        ImpactSequence++;

        StopProjectile();

        ImpactDespawnTimer =
            TickTimer.CreateFromSeconds(
                Runner,
                impactPresentationDuration);
    }*/

    private void ApplyRotationFromVelocity()
    {
        if (!_alignRotationToVelocity)
            return;

        Vector2 direction =
            NormalizeDirection(
                _velocity);

        if (direction ==
            Vector2.zero)
        {
            return;
        }

        float angle =
            Mathf.Atan2(
                direction.y,
                direction.x) *
            Mathf.Rad2Deg;

        transform.rotation =
            Quaternion.Euler(
                0f,
                0f,
                angle);
    }

    protected static Vector2 NormalizeDirection(
        Vector2 direction)
    {
        if (direction.sqrMagnitude <=
            0.0001f)
        {
            return Vector2.zero;
        }

        return direction.normalized;
    }


    private static Vector2 ToLocalDirectionSpace(
        Vector2 worldVector,
        Vector2 forward)
    {
        Vector2 perpendicular =
            new Vector2(
                -forward.y,
                forward.x);

        return new Vector2(
            Vector2.Dot(
                worldVector,
                forward),

            Vector2.Dot(
                worldVector,
                perpendicular));
    }


    private static Vector2 FromLocalDirectionSpace(
        Vector2 localVector,
        Vector2 forward)
    {
        if (forward ==
            Vector2.zero)
        {
            return Vector2.zero;
        }

        Vector2 perpendicular =
            new Vector2(
                -forward.y,
                forward.x);

        return
            forward *
            localVector.x +
            perpendicular *
            localVector.y;
    }
}
