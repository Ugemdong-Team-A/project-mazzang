using UnityEngine;

public class PredictedProjectile : MonoBehaviour
{
    [SerializeField]
    private ProjectileVisual visual;

    [SerializeField]
    private ProjectileMovement movement;

    [SerializeField]
    private ProjectileTrail trail;

    public void Initialize(
        WeaponFirePose firePose,
        ProjectileLaunchSettings settings,
        Vector2? optionalDir = null
        )
    {
        transform.position = firePose.Origin;

        if (optionalDir.HasValue)
            transform.right = optionalDir.Value;
        else transform.right = firePose.Direction;

        movement.Initialize(
            firePose,
            settings,
            optionalDir);

        movement.OnExpired += () => Destroy(gameObject);

        visual.Initialize();

        trail.Begin(
            transform.position,
            transform);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    /*void Update()
    {
        
    }*/
}
