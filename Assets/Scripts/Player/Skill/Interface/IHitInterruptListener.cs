using UnityEngine;

public interface IHitInterruptListener
{
    /// <summary>�÷��̾ ���ظ� �޾� ��ų/�ൿ�� �ߴ��ؾ� �� �� ȣ��˴ϴ�.</summary>
    /// <param name="info">�ǰ� �ƶ�(����/�и� ���� ��)</param>
    void OnPlayerHitInterrupt(PlayerHit.HitInterruptInfo info);
}
