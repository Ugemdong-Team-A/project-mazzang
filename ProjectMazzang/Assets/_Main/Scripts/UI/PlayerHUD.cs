/*using UnityEngine;

public sealed class PlayerHUD :
    MonoBehaviour
{
    [Header("Skills")]
    [SerializeField]
    private SkillSlotUI skill1Slot;

    [SerializeField]
    private SkillSlotUI skill2Slot;


    public void Bind(
        PlayerSkillController skillController)
    {
        skill1Slot?.Bind(
            skillController,
            SkillSlot.Skill1);

        skill2Slot?.Bind(
            skillController,
            SkillSlot.Skill2);
    }


    public void Unbind()
    {
        skill1Slot?.Unbind();
        skill2Slot?.Unbind();
    }


    public void Show()
    {
        gameObject.SetActive(
            true);
    }


    public void Hide()
    {
        gameObject.SetActive(
            false);
    }
}*/