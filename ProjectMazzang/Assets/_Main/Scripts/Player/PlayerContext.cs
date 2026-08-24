/*using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PlayerContext
{
    private readonly Dictionary<Type, object> _units = new();

    public GameObject Owner { get; }

    public Transform Transform =>
        Owner.transform;


    public PlayerContext(
        GameObject owner)
    {
        Owner = owner != null
            ? owner
            : throw new ArgumentNullException(
                nameof(owner));
    }


    // =========================================================
    // Unit
    // =========================================================

    public void Register<T>(
        T unit)
        where T : class, IPlayerContextUnit
    {
        if (unit == null)
        {
            throw new ArgumentNullException(
                nameof(unit));
        }

        Type type =
            typeof(T);

        if (_units.ContainsKey(type))
        {
            Debug.LogError(
                $"PlayerContext에 {type.Name}이(가) " +
                "이미 등록되어 있습니다.",
                Owner);

            return;
        }

        _units.Add(
            type,
            unit);
    }


    public bool TryGet<T>(
        out T unit)
        where T : class, IPlayerContextUnit
    {
        if (_units.TryGetValue(
                typeof(T),
                out object value) &&
            value is T typedUnit)
        {
            unit = typedUnit;
            return true;
        }

        unit = null;
        return false;
    }


    public T Get<T>()
        where T : class, IPlayerContextUnit
    {
        if (TryGet(
                out T unit))
        {
            return unit;
        }

        Debug.LogError(
            $"PlayerContext에서 {typeof(T).Name}을(를) " +
            "찾을 수 없습니다.",
            Owner);

        return null;
    }
}
*/