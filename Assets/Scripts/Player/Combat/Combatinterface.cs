using UnityEngine;

/// <summary>패링(위빙) 성공 시 적 오브젝트가 받는 콜백</summary>
public interface IParryable
{
    void OnParried(Vector3 parrySourcePosition);
}

/// <summary>플레이어/적이 공격을 맞을 때 호출되는 최소 인터페이스</summary>
public interface IDamageable
{
    /// <param name="hitDirFromPlayer">플레이어 → 적 방향(정규화 권장)</param>
    void ApplyHit(float damage, float knockback, Vector2 hitDirFromPlayer, GameObject attacker);
}
