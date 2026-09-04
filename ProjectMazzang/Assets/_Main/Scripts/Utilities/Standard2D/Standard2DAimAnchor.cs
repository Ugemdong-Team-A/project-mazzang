using UnityEngine;

/// <summary>
/// CharSetup이 생성하는 표준 상체 조준 기준점.
/// Player 모듈을 모르며 RAP와 그 아래 Weapon Socket의 리그 계약만 보관한다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Mazzang/Animation/Standard 2D Aim Anchor")]
public sealed class Standard2DAimAnchor : MonoBehaviour
{
    [SerializeField, HideInInspector]
    private Transform _weaponSocket;

    public Transform ReferenceBone =>
        transform.parent;

    public Transform ResolvedAimPivot =>
        transform;

    public Transform WeaponSocket =>
        _weaponSocket;

    public bool IsValid =>
        ReferenceBone != null &&
        _weaponSocket != null &&
        _weaponSocket.parent == transform;

    public bool Synchronize(
        Transform weaponSocket)
    {
        if (_weaponSocket == weaponSocket)
            return false;

        _weaponSocket = weaponSocket;
        return true;
    }
}
