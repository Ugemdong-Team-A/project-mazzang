using UnityEngine;

public sealed class HeldWeaponView :
    MonoBehaviour
{
    public const string RuntimeViewName = "HeldWeaponView";

    [Header("Anchors")]
    [SerializeField]
    private Transform muzzle;

    [SerializeField]
    private Transform leftHandGrip;

    [SerializeField]
    private Transform rightHandGrip;

    private WeaponPose _pose;

    public Transform Muzzle =>
        muzzle;

    public Transform LeftHandGrip =>
        leftHandGrip;

    public Transform RightHandGrip =>
        rightHandGrip;

    public WeaponPose Pose =>
        _pose;

    public void Initialize(
        Transform socket,
        int sortingOrder)
    {
        if (socket == null)
            return;

        WeaponPose pose =
            WeaponPose.GetOrCreate(socket);

        Initialize(
            pose,
            sortingOrder);
    }

    public void Initialize(
        WeaponPose pose,
        int sortingOrder)
    {
        if (pose == null)
            return;

        _pose = pose;
        name = RuntimeViewName;
        _pose.Attach(this);

        foreach (Collider2D collider
                 in GetComponentsInChildren<Collider2D>(true))
        {
            collider.enabled =
                false;
        }

        foreach (SpriteRenderer renderer
                 in GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sortingOrder =
                sortingOrder;
        }
    }


    public void SetMirrored(
        bool mirrored)
    {
        _pose?.SetMirrored(mirrored);
    }

    public void PlayAnimation(
        AnimationClip clip)
    {
        _pose?.Play(clip);
    }

    public void StopAnimation()
    {
        _pose?.Stop();
    }
}
