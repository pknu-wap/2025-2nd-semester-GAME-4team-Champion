using UnityEngine;

public interface IHitInterruptListener
{
    /// <summary>플레이어가 피해를 받아 스킬/행동을 중단해야 할 때 호출됩니다.</summary>
    /// <param name="info">피격 맥락(가드/패링 여부 등)</param>
    void OnPlayerHitInterrupt(PlayerHit.HitInterruptInfo info);
}
