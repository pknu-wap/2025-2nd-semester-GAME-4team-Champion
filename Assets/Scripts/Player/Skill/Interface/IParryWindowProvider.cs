using UnityEngine;

public interface IParryWindowProvider
{
    /// ������ �и� ������ ����ȿ â������ (��Ʈ ������ ����)
    bool IsParryWindowActive { get; }

    /// �и� ������ Ȯ���Ǿ��� �� �Ҹ��ϴ�(ī����/��Ÿ�� ���� �� ��ų ���� ó��).
    void OnParrySuccess();
}
