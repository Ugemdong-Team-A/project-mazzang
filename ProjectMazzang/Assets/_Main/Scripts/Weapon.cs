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

    [Networked]
    private PlayerRef PickupBlockedPlayer
    {
        get;
        set;
    }

    [Networked]
    private TickTimer PickupBlockedTimer
    {
        get;
        set;
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

        PickupBlockedPlayer =
            PlayerRef.None;

        PickupBlockedTimer =
            TickTimer.None;

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

        /*PlayerWeaponController controller =
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
            angle;*/

        rb.linearVelocity =
            Vector2.zero;

        rb.angularVelocity =
            0f;
    }


    // =========================================================
    // Equip / Drop
    // =========================================================

    public bool CanBePickedUpBy(
        PlayerRef player)
    {
        if (IsEquipped)
            return false;

        if (player == PlayerRef.None)
            return false;

        bool pickupBlocked =
            !PickupBlockedTimer
                .ExpiredOrNotRunning(Runner);

        if (pickupBlocked &&
            player == PickupBlockedPlayer)
        {
            return false;
        }

        return true;
    }


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

        PlayerRef holderPlayer =
            holder.InputAuthority;

        if (!CanBePickedUpBy(
                holderPlayer))
        {
            return false;
        }

        PlayerWeaponController weaponController =
            holder.GetComponent<PlayerWeaponController>();

        if (weaponController == null ||
            weaponController.WeaponSocket == null)
        {
            return false;
        }

        Holder =
            holder;

        Vector3 worldScale =
            transform.lossyScale;

        Transform socket =
            weaponController.WeaponSocket;

        transform.SetParent(
            socket,
            false);

        transform.localPosition =
            Vector3.zero;

        transform.localRotation =
            Quaternion.identity;

        transform.localScale =
            Utility.CalculateLocalScaleForWorldScale(
                worldScale,
                socket.lossyScale);

        transform.localRotation =
            Quaternion.identity;

        PickupBlockedPlayer =
            PlayerRef.None;

        PickupBlockedTimer =
            TickTimer.None;

        ApplyEquippedAuthorityState();
        ApplyLocalPickupState();

        return true;
    }

    public void Drop(
        PlayerRef previousHolder,
        Vector2 velocity,
        float repickupBlockDuration)
    {
        if (!HasStateAuthority)
            return;

        transform.SetParent(
            null,
            true);

        transform.localScale = Vector3.one;

        Holder =
            null;

        if (previousHolder != PlayerRef.None &&
            repickupBlockDuration > 0f)
        {
            PickupBlockedPlayer =
                previousHolder;

            PickupBlockedTimer =
                TickTimer.CreateFromSeconds(
                    Runner,
                    repickupBlockDuration);
        }
        else
        {
            PickupBlockedPlayer =
                PlayerRef.None;

            PickupBlockedTimer =
                TickTimer.None;
        }

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

        rb.angularVelocity =
            0f;
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