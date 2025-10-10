using UnityEngine;

public interface IPlayerSkill
{
    string SkillName { get; }
    /// 스킬 전체 길이(선딜+활성+후딜)
    float GetTotalDuration();

    /// PlayerSkills에서 통일된 방식으로 호출
    bool TryCastSkill(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a);
}
