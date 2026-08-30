using UnityEngine;

[CreateAssetMenu(
    menuName = "Mazzang/Data/Player/Stats",
    fileName = "PlayerStatsData")]
public sealed class PlayerStatsData :
    ScriptableObject
{
    [Header("Movement")]
    [Min(0f)]
    [SerializeField]
    private float moveSpeed = 7f;

    [Header("Health")]
    [Min(1)]
    [SerializeField]
    private int maxHealth = 100;

    public float MoveSpeed =>
        Mathf.Max(
            0f,
            moveSpeed);

    public int MaxHealth =>
        Mathf.Max(
            1,
            maxHealth);
}
