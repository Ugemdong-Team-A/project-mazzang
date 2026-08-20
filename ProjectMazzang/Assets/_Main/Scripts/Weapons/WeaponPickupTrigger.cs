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

        Debug.Log(
            $"[PICKUP TRIGGER] weapon={weapon} " +
            $"authority={weapon?.HasStateAuthority} " +
            $"equipped={weapon?.IsEquipped} " +
            $"other={other.name}",
            this);

        // 기존 코드...


        if (weapon == null ||
            !weapon.HasStateAuthority ||
            weapon.IsEquipped)
        {
            return;
        }

        PlayerWeaponController controller =
            other.GetComponentInParent<
                PlayerWeaponController>();

        if (controller == null)
            return;

        if (!weapon.CanBePickedUpBy(
                controller.Object.InputAuthority))
        {
            return;
        }

        controller.TryEquipWeapon(
            weapon);
    }
}