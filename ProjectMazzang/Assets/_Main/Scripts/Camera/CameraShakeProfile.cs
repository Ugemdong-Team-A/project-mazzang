using UnityEngine;

[CreateAssetMenu(
    fileName = "CameraShakeProfile",
    menuName = "Mazzang/Data/Camera/Shake Profile")]
public sealed class CameraShakeProfile :
    ScriptableObject
{
    [Min(0f)]
    [SerializeField]
    private float force = 0.2f;

    [Min(0.01f)]
    [SerializeField]
    private float duration = 0.2f;

    [Min(0f)]
    [SerializeField]
    private float maxDistance = 15f;

    [Range(0f, 1f)]
    [SerializeField]
    private float minimumDistanceFactor = 0.25f;

    [Min(0f)]
    [SerializeField]
    private float minimumInterval;

    public float Force =>
        force;

    public float Duration =>
        duration;

    public float MaxDistance =>
        maxDistance;

    public float MinimumDistanceFactor =>
        minimumDistanceFactor;

    public float MinimumInterval =>
        minimumInterval;
}
