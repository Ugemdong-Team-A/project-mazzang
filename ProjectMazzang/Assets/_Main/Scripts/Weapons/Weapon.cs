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

    [SerializeField]
    private HeldWeaponView presentationTemplate;


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

    public HeldWeaponView HeldView =>
        _heldView;

    public bool TryGetHeldMuzzlePosition(
        out Vector2 position)
    {
        if (_heldView != null &&
            _heldView.Muzzle != null)
        {
            position =
                _heldView.Muzzle.position;

            return true;
        }

        position =
            default;

        return false;
    }

    public virtual bool ConsumesParryInput =>
        false;


    private int _worldSortingOrder;

    private HeldWeaponView _heldView;

    private NetworkRigidbody _networkRigidbody;


    // =========================================================
    // Unity
    // =========================================================

    protected virtual void Awake()
    {
        _networkRigidbody =
            GetComponent<NetworkRigidbody>();

        if (_networkRigidbody != null)
        {
            _networkRigidbody.SyncParent = false;
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
                    ? rb.position
                    : (Vector2)transform.position,
                rb != null
                    ? rb.rotation
                    : transform.eulerAngles.z,
                rb != null
                    ? rb.linearVelocity
                    : Vector2.zero);
        }
    }


    public override void Despawned(
        NetworkRunner runner,
        bool hasState)
    {
        DestroyHeldView();
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

        IWeaponHandler weaponHandler =
            holder.GetComponent<IWeaponHandler>();

        if (weaponHandler == null ||
            weaponHandler.WeaponSocket == null)
        {
            return false;
        }

        Holder =
            holder;

        ApplyLocalHolderState();

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
        Vector2 position,
        float angle,
        Vector2 velocity,
        float repickupBlockDuration)
    {
        if (!HasStateAuthority)
            return;

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
            position,
            angle,
            velocity);

        ApplyLocalHolderState();
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
        Vector2 position,
        float angle,
        Vector2 velocity)
    {
        if (rb == null)
            return;

        rb.bodyType =
            RigidbodyType2D.Dynamic;

        if (_networkRigidbody != null &&
            Object != null)
        {
            _networkRigidbody.Teleport(
                new Vector3(
                    position.x,
                    position.y,
                    transform.position.z),
                Quaternion.Euler(
                    0f,
                    0f,
                    angle));
        }
        else
        {
            rb.position =
                position;

            rb.rotation =
                angle;
        }

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
            EnsureHeldView();

            if (presentationTemplate != null)
            {
                presentationTemplate.gameObject
                    .SetActive(
                        false);
            }
        }
        else
        {
            DestroyHeldView();

            if (presentationTemplate != null)
            {
                presentationTemplate.gameObject
                    .SetActive(
                        true);
            }
        }
    }


    private void EnsureHeldView()
    {
        if (_heldView != null)
            return;

        if (Holder == null ||
            !Holder.TryGetComponent(
                out IWeaponHandler handler))
        {
            return;
        }

        if (handler.WeaponSocket == null ||
            presentationTemplate == null)
        {
            return;
        }

        _heldView =
            Instantiate(
                presentationTemplate);

        _heldView.name =
            $"{name} Held View";

        _heldView.gameObject
            .SetActive(
                true);

        _heldView.Initialize(
            handler.WeaponSocket,
            handler.WeaponSortingOrder);
    }


    public void RefreshHeldPresentation(
        bool mirrored)
    {
        if (!IsEquipped)
            return;

        EnsureHeldView();

        if (_heldView != null)
        {
            _heldView.SetMirrored(
                mirrored);
        }
    }


    private void DestroyHeldView()
    {
        if (_heldView == null)
            return;

        _heldView.gameObject
            .SetActive(
                false);

        Destroy(
            _heldView.gameObject);

        _heldView =
            null;
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

        sortingGroup.sortingOrder =
            _worldSortingOrder;
    }


    // =========================================================
    // Weapon Action
    // =========================================================

    public abstract bool TryUse(
        Vector2 origin,
        Vector2 direction,
        bool mirrored,
        float attackDamageMultiplier);

    public virtual bool TryUseSecondary(
        Vector2 origin,
        Vector2 direction,
        bool mirrored)
    {
        return false;
    }
}
