using UnityEngine;

[CreateAssetMenu(fileName = "SpeedPadSettings", menuName = "Game/Field/Speed Pad Settings")]
public class SpeedPadSettings : ScriptableObject
{
    [Header("Scale Limits")]
    [Min(1f)] public float minScale = 1f;      // 장판 위 기본 배수
    [Min(1f)] public float maxScale = 2.5f;    // 연타 한계 배수

    [Header("Mash (Q 연타)")]
    [Min(0f)] public float mashStep = 0.15f;   // Q 1회당 추가 배수
    [Min(0f)] public float mashDecayPerSec = 0.5f; // 장판 위에서도 누르지 않으면 초당 이만큼 감소

    [Header("Exit Decay")]
    [Min(0.05f)] public float exitEaseTime = 1.0f; // 장판을 벗어난 후 1배로 서서히 복귀하는 시간
}
