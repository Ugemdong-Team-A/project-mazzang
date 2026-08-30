using Fusion;
using UnityEngine;

public interface IWeaponHandler
{
    NetworkObject Object { get; }

    NetworkObject EquippedWeaponObject { get; }

    bool HasEquippedWeapon { get; }

    bool ConsumesParryInput { get; }

    Weapon EquippedWeapon { get; }

    Transform WeaponSocket { get; }

    int WeaponSortingOrder { get; }

    Vector2 WeaponDirection { get; }

    bool TryEquipWeapon(Weapon weapon);

    bool TryDropWeapon();
}
