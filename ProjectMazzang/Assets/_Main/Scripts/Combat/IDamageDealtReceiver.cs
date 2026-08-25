/// <summary>
/// State Authority가 확정한 실제 피해량을 보상으로 받는 객체입니다.
/// </summary>
public interface IDamageDealtReceiver
{
    void ReceiveDamageDealt(
        int appliedDamage);
}
