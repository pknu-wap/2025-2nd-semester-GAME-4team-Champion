using UnityEngine;

[CreateAssetMenu(fileName = "CameraFXProfile", menuName = "Game/Camera FX Profile")]
public class CameraFXProfile : ScriptableObject
{
    [Header("Zoom (OrthographicSize)")]
    public bool enableZoom = true;
    [Min(0.01f)] public float zoomToSize = 3.5f;   // ��ǥ Ortho Size (�������� ����)
    [Min(0f)] public float zoomInTime = 0.15f;     // ��-�� �ð�
    [Min(0f)] public float holdTime = 0.20f;       // ���� �ð�
    [Min(0f)] public float resetTime = 0.25f;      // ���� ������� ���� �ð�
    public AnimationCurve easeIn = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve easeOut = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Shake (Cinemachine)")]
    public bool enableShake = true;
    [Min(0f)] public float shakeAmplitude = 1.5f;  // ��� ����
    [Min(0f)] public float shakeFrequency = 1.0f;  // ��� �ֱ�
    [Min(0f)] public float shakeDuration = 0.12f;  // ��� ����

    [Header("Options")]
    public bool resetZoomIfAlreadyZooming = false; // ��Ʈ���� �� ���� ���� ���� �ٽ�
}
