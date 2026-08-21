using UnityEngine;

public sealed class MapOutZone : MonoBehaviour
{
    private void OnTriggerEnter2D(
        Collider2D other)
    {
        PlayerHealth health =
            other.GetComponentInParent<PlayerHealth>();

        if (health == null)
            return;

        health.ApplyMapOut();
    }
}