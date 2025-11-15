using UnityEngine;

[CreateAssetMenu(fileName = "CameraFXProfile", menuName = "Game/Camera FX Profile")]
public class CameraFXProfile : ScriptableObject
{
    [Header("Zoom (OrthographicSize)")]
    public bool enableZoom = true;
    [Min(0.01f)] public float zoomToSize = 3.5f;   // 목표 Ortho Size (작을수록 줌인)
    [Min(0f)] public float zoomInTime = 0.15f;     // 줌-인 시간
    [Min(0f)] public float holdTime = 0.20f;       // 유지 시간
    [Min(0f)] public float resetTime = 0.25f;      // 원래 사이즈로 리셋 시간
    public AnimationCurve easeIn = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve easeOut = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Shake (Cinemachine)")]
    public bool enableShake = true;
    [Min(0f)] public float shakeAmplitude = 1.5f;  // 흔들 세기
    [Min(0f)] public float shakeFrequency = 1.0f;  // 흔들 주기
    [Min(0f)] public float shakeDuration = 0.12f;  // 흔들 지속

    [Header("Options")]
    public bool resetZoomIfAlreadyZooming = false; // 재트리거 시 기존 줌을 끊고 다시
}
