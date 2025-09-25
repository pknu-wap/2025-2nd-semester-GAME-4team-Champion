using UnityEngine;

/// <summary>�и�(����) ���� �� �� ������Ʈ�� �޴� �ݹ�</summary>
public interface IParryable
{
    void OnParried(Vector3 parrySourcePosition);
}

/// <summary>�÷��̾�/���� ������ ���� �� ȣ��Ǵ� �ּ� �������̽�</summary>
public interface IDamageable
{
    /// <param name="hitDirFromPlayer">�÷��̾� �� �� ����(����ȭ ����)</param>
    void ApplyHit(float damage, float knockback, Vector2 hitDirFromPlayer, GameObject attacker);
}
