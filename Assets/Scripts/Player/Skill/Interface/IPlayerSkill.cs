using UnityEngine;

public interface IPlayerSkill
{
    string SkillName { get; }
    /// ��ų ��ü ����(����+Ȱ��+�ĵ�)
    float GetTotalDuration();

    /// PlayerSkills���� ���ϵ� ������� ȣ��
    bool TryCastSkill(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a);
}
