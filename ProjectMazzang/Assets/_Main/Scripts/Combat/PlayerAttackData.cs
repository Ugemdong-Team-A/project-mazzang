using UnityEngine;

[CreateAssetMenu(
    menuName = "Game/Combat/Player Attack Data",
    fileName = "PlayerAttackData")]
public sealed class PlayerAttackData :
    ScriptableObject
{
    [SerializeField]
    private PlayerAttackDefinition definition;


    public PlayerAttackDefinition Definition =>
        definition;

    public bool IsValid =>
        definition.IsValid;
}
