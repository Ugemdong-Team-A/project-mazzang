using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.Rendering;

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


    [Header("Presentation")]
    [SerializeField]
    private SortingGroup sortingGroup;


    [Header("Hand Grip")]
    [SerializeField]
    private Transform leftHandGrip;

    [SerializeField]
    private Transform rightHandGrip;


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

    public Transform LeftHandGrip =>
        leftHandGrip;

    public Transform RightHandGrip =>
        rightHandGrip;


    private int _worldSortingOrder;

    private Vector3 _worldScale;


    // =========================================================
    // Unity
    // =========================================================

    protected virtual void Awake()
    {
        NetworkRigidbody networkRigidbody =
            GetComponent<NetworkRigidbody>();

        if (networkRigidbody != null)
        {
            networkRigidbody.SyncParent = false;
        }

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

        if (sortingGroup == null)
        {
            sortingGroup =
                GetComponentInChildren<
                    SortingGroup>(true);
        }

        if (sortingGroup != null)
        {
            _worldSortingOrder =
                sortingGroup.sortingOrder;
        }

        _worldScale =
            transform.lossyScale;
    }


    // =========================================================
    // Fusion
    // =========================================================

    public override void Spawned()
    {
        ApplyLocalHolderState();
        ApplyLocalPickupState();
        ApplyLocalSortingState();

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

        ApplyEquippedAuthorityPose();

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

        ApplyEquippedPresentation();

        PickupBlockedPlayer =
            PlayerRef.None;

        PickupBlockedTimer =
            TickTimer.None;

        ApplyEquippedAuthorityState();
        ApplyLocalPickupState();
        ApplyLocalSortingState();

        return true;
    }

    public void Drop(
        PlayerRef previousHolder,
        Vector2 velocity,
        float repickupBlockDuration)
    {
        if (!HasStateAuthority)
            return;

        ApplyWorldPresentation();

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
        ApplyLocalSortingState();
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
        ApplyLocalHolderState();
        ApplyLocalPickupState();
        ApplyLocalSortingState();
    }


    private void ApplyLocalHolderState()
    {
        if (IsEquipped)
        {
            ApplyEquippedPresentation();
        }
        else
        {
            ApplyWorldPresentation();
        }
    }


    private void ApplyEquippedPresentation()
    {
        if (Holder == null ||
            !Holder.TryGetComponent(
                out PlayerWeaponController controller))
        {
            return;
        }

        Transform socket =
            controller.WeaponSocket;

        if (socket == null)
            return;

        if (transform.parent != null)
        {
            transform.SetParent(
                null,
                true);
        }

        transform.SetPositionAndRotation(
            socket.position,
            Quaternion.Euler(
                0f,
                0f,
                ResolveSocketWorldAngle(
                    socket)));

        transform.localScale =
            _worldScale;
    }


    public void RefreshEquippedPresentation()
    {
        if (!IsEquipped)
            return;

        ApplyEquippedPresentation();
    }


    private void ApplyEquippedAuthorityPose()
    {
        if (rb == null ||
            Holder == null ||
            !Holder.TryGetComponent(
                out PlayerWeaponController controller) ||
            controller.WeaponSocket == null)
        {
            return;
        }

        Transform socket =
            controller.WeaponSocket;

        rb.position =
            socket.position;

        rb.rotation =
            ResolveSocketWorldAngle(
                socket);
    }


    private static float ResolveSocketWorldAngle(
        Transform socket)
    {
        Vector3 forward =
            socket.TransformVector(
                Vector3.right);

        if (forward.sqrMagnitude <= 0.0001f)
        {
            return socket.eulerAngles.z;
        }

        return Mathf.Atan2(
                   forward.y,
                   forward.x) *
               Mathf.Rad2Deg;
    }


    private void ApplyWorldPresentation()
    {
        if (transform.parent != null)
        {
            transform.SetParent(
                null,
                true);
        }

        transform.localScale =
            _worldScale;
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
    // Presentation / Sorting
    // =========================================================

    private void ApplyLocalSortingState()
    {
        if (sortingGroup == null)
            return;

        if (!IsEquipped ||
            Holder == null)
        {
            sortingGroup.sortingOrder =
                _worldSortingOrder;

            return;
        }

        if (!Holder.TryGetComponent(
                out PlayerWeaponController weaponController))
        {
            sortingGroup.sortingOrder =
                _worldSortingOrder;

            return;
        }

        sortingGroup.sortingOrder =
            weaponController.WeaponSortingOrder;
    }


    // =========================================================
    // Weapon Action
    // =========================================================

    public abstract bool TryUse(
        Vector2 origin,
        Vector2 direction);
}
