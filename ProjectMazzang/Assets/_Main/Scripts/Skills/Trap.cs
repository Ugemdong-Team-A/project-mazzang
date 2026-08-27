using UnityEngine;
using Fusion;

public class Trap : Deployable
{
    [SerializeField] 
    private Collider2D triggerCollider;

    [SerializeField]
    private bool triggerOnce;

    [Min(0f)]
    [SerializeField]
    private float retriggerCooldown;

    [SerializeField]
    private float damage;

    // [SerializeField]

}
