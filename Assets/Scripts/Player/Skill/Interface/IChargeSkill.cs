using UnityEngine;

public interface IChargeSkill : IPlayerSkill
{
    /// ���� ����(���� �� true). �ʿ� ������ PlayerSkills�� ����.
    bool TryStartCharge(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a);

    /// ���� ����(�ߵ� �Ǵ� ��Ҵ� ��ų�� �Ǵ�)
    void ReleaseCharge();
}
