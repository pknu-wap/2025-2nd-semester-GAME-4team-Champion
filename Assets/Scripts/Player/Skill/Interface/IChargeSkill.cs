using UnityEngine;

public interface IChargeSkill : IPlayerSkill
{
    /// 차지 시작(성공 시 true). 필요 참조는 PlayerSkills가 주입.
    bool TryStartCharge(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a);

    /// 차지 해제(발동 또는 취소는 스킬이 판단)
    void ReleaseCharge();
}
