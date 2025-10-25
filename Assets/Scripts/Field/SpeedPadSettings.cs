using UnityEngine;

[CreateAssetMenu(fileName = "SpeedPadSettings", menuName = "Game/Field/Speed Pad Settings")]
public class SpeedPadSettings : ScriptableObject
{
    [Header("Scale Limits")]
    [Min(1f)] public float minScale = 1f;      // ���� �� �⺻ ���
    [Min(1f)] public float maxScale = 2.5f;    // ��Ÿ �Ѱ� ���

    [Header("Mash (Q ��Ÿ)")]
    [Min(0f)] public float mashStep = 0.15f;   // Q 1ȸ�� �߰� ���
    [Min(0f)] public float mashDecayPerSec = 0.5f; // ���� �������� ������ ������ �ʴ� �̸�ŭ ����

    [Header("Exit Decay")]
    [Min(0.05f)] public float exitEaseTime = 1.0f; // ������ ��� �� 1��� ������ �����ϴ� �ð�
}
