using UnityEngine;

public sealed class HeldWeaponView :
    MonoBehaviour
{
    [Header("Anchors")]
    [SerializeField]
    private Transform muzzle;

    [SerializeField]
    private Transform leftHandGrip;

    [SerializeField]
    private Transform rightHandGrip;

    public Transform Muzzle =>
        muzzle;

    public Transform LeftHandGrip =>
        leftHandGrip;

    public Transform RightHandGrip =>
        rightHandGrip;

    public void Initialize(
        Transform socket,
        int sortingOrder)
    {
        if (socket == null)
            return;

        transform.SetParent(
            socket,
            false);

        transform.localPosition =
            Vector3.zero;

        transform.localRotation =
            Quaternion.identity;

        transform.localScale =
            Vector3.one;

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
        transform.localScale =
            new Vector3(
                1f,
                mirrored ? -1f : 1f,
                1f);
    }
}
