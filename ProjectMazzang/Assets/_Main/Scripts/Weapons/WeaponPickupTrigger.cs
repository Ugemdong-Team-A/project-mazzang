using UnityEngine;

public sealed class WeaponPickupTrigger :
    MonoBehaviour
{
    [SerializeField]
    private Weapon weapon;

    [SerializeField]
    private Collider2D triggerCollider;


    private void Awake()
    {
        if (weapon == null)
        {
            weapon =
                GetComponentInParent<Weapon>();
        }

        if (triggerCollider == null)
        {
            triggerCollider =
                GetComponent<Collider2D>();
        }
    }


    public void SetPickupEnabled(
        bool enabled)
    {
        if (triggerCollider != null)
        {
            triggerCollider.enabled =
                enabled;
        }
    }


    private void OnTriggerEnter2D(
        Collider2D other)
    {
        if (weapon == null ||
            !weapon.HasStateAuthority ||
            weapon.IsEquipped)
        {
            return;
        }

        IWeaponHandler handler =
            other.GetComponentInParent<
                IWeaponHandler>();

        if (handler == null)
            return;

        if (!weapon.CanBePickedUpBy(
                handler.Object.InputAuthority))
        {
            return;
        }

        handler.TryEquipWeapon(
            weapon);
    }
}
