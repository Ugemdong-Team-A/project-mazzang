using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(NetworkRigidbody))]
public abstract class Weapon :
    NetworkBehaviour
{
    [Header("World Physics")]
    [SerializeField]
    private Rigidbody2D rb;

    [SerializeField]
    private Collider2D worldCollider;

    [SerializeField]
    private WeaponPickupTrigger pickupTrigger;

    [Header("Hand IK")]
    [SerializeField]
    private Transform leftHandGrip;

    [SerializeField]
    private Transform rightHandGrip;

    public Transform LeftHandGrip =>
        leftHandGrip;

    public Transform RightHandGrip =>
        rightHandGrip;

    // =========================================================
    // Network State
    // =========================================================

    [Networked,
     OnChangedRender(nameof(OnHolderChanged))]
    public NetworkObject Holder
    {
        get;
        private set;
    }


    // =========================================================
    // State
    // =========================================================

    public bool IsEquipped =>
        Holder != null;


    // =========================================================
    // Unity
    // =========================================================

    protected virtual void Awake()
    {
        if (rb == null)
        {
            rb =
                GetComponent<Rigidbody2D>();
        }

        if (worldCollider == null)
        {
            worldCollider =
                GetComponent<Collider2D>();
        }

        if (pickupTrigger == null)
        {
            pickupTrigger =
                GetComponentInChildren<
                    WeaponPickupTrigger>(true);
        }
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        ApplyLocalPickupState();

        if (!HasStateAuthority)
            return;

        if (!IsEquipped)
        {
            ApplyWorldAuthorityState(
                rb != null
                    ? rb.linearVelocity
                    : Vector2.zero);
        }
    }


    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority ||
            !IsEquipped ||
            rb == null)
        {
            return;
        }

        PlayerWeaponController controller =
            Holder.GetComponent<
                PlayerWeaponController>();

        if (controller == null)
            return;

        if (!controller.TryGetWeaponPose(
                out Vector2 position,
                out float angle))
        {
            return;
        }

        rb.position =
            position;

        rb.rotation =
            angle;

        rb.linearVelocity =
            Vector2.zero;

        rb.angularVelocity =
            0f;
    }


    // =========================================================
    // Equip / Drop
    // =========================================================

    public bool TryEquip(
        NetworkObject holder)
    {
        if (!HasStateAuthority)
            return false;

        if (holder == null ||
            IsEquipped)
        {
            return false;
        }

        Holder =
            holder;

        ApplyEquippedAuthorityState();
        ApplyLocalPickupState();

        return true;
    }


    public void Drop(
        Vector2 velocity)
    {
        if (!HasStateAuthority)
            return;

        Holder =
            null;

        ApplyWorldAuthorityState(
            velocity);

        ApplyLocalPickupState();
    }


    private void ApplyEquippedAuthorityState()
    {
        if (rb == null)
            return;

        rb.bodyType =
            RigidbodyType2D.Kinematic;

        rb.linearVelocity =
            Vector2.zero;

        rb.angularVelocity =
            0f;
    }


    private void ApplyWorldAuthorityState(
        Vector2 velocity)
    {
        if (rb == null)
            return;

        rb.bodyType =
            RigidbodyType2D.Dynamic;

        rb.linearVelocity =
            velocity;
    }


    // =========================================================
    // Presentation / Local Collision State
    // =========================================================

    private void OnHolderChanged()
    {
        ApplyLocalPickupState();
    }


    private void ApplyLocalPickupState()
    {
        bool worldState =
            !IsEquipped;

        if (worldCollider != null)
        {
            worldCollider.enabled =
                worldState;
        }

        if (pickupTrigger != null)
        {
            pickupTrigger.SetPickupEnabled(
                worldState);
        }
    }


    // =========================================================
    // Weapon Action
    // =========================================================

    public abstract bool TryUse(
        PlayerRef attacker,
        Vector2 origin,
        Vector2 direction);
}
