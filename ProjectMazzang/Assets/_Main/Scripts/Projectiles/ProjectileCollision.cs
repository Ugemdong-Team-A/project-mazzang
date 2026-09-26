using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 투사체 이동 구간에서 가장 가까운 충돌만 조회하는 수동 컴포넌트입니다.
/// 이동, 피해 적용과 충돌 연출은 호출자가 결정합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProjectileCollision : MonoBehaviour
{
    private const int InitialHitCapacity =
        16;

    private readonly List<RaycastHit2D> _hitBuffer =
        new(InitialHitCapacity);

    private Transform _ownerRoot;
    private NetworkObject _ownerObject;
    private NetworkObject _source;
    private ContactFilter2D _contactFilter;
    private float _collisionRadius;
    private bool _initialized;


    public void Initialize(
        Transform ownerRoot,
        NetworkObject ownerObject,
        NetworkObject source,
        LayerMask collisionMask,
        float collisionRadius)
    {
        _ownerRoot =
            ownerRoot;

        _ownerObject =
            ownerObject;

        _source =
            source;

        _contactFilter =
            new ContactFilter2D
            {
                useTriggers =
                    Physics2D.queriesHitTriggers
            };

        _contactFilter.SetLayerMask(
            collisionMask);

        _collisionRadius =
            Mathf.Max(
                0.001f,
                collisionRadius);

        _initialized =
            true;
    }


    public bool TrySweep(
        Vector2 start,
        Vector2 displacement,
        out RaycastHit2D nearestHit)
    {
        nearestHit =
            default;

        if (!_initialized)
            return false;

        float distance =
            displacement.magnitude;

        if (distance <= 0.0001f)
            return false;

        Vector2 direction =
            displacement /
            distance;

        int hitCount =
            Physics2D.CircleCast(
                start,
                _collisionRadius,
                direction,
                _contactFilter,
                _hitBuffer,
                distance);

        bool found =
            false;

        float nearestDistance =
            float.MaxValue;

        for (int i = 0;
             i < hitCount;
             i++)
        {
            RaycastHit2D hit =
                _hitBuffer[i];

            Collider2D candidate =
                hit.collider;

            if (candidate == null ||
                ShouldIgnore(
                    candidate) ||
                hit.distance >= nearestDistance)
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
    }


    public void SetSource(
        NetworkObject source)
    {
        _source =
            source;
    }


    private bool ShouldIgnore(
        Collider2D candidate)
    {
        Transform candidateTransform =
            candidate.transform;

        if (_ownerRoot != null &&
            (candidateTransform == _ownerRoot ||
             candidateTransform.IsChildOf(
                 _ownerRoot)))
        {
            return true;
        }

        NetworkObject targetObject =
            candidate.GetComponentInParent<
                NetworkObject>();

        if (targetObject == null)
            return false;

        return targetObject == _ownerObject ||
               targetObject == _source;
    }
}
