using System.Collections;
using System.Reflection;
using UnityEngine;
using Unity.Cinemachine;

public class CameraFXController : MonoBehaviour
{
    [Header("Cinemachine v3")]
    [SerializeField] private CinemachineCamera vcam;                // ✅ v3 카메라
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Header("Defaults")]
    [SerializeField] private float defaultOrthoSize = 10f;
    [SerializeField] private bool readDefaultFromVcamOnStart = true;

    public float GetCurrentSize() => vcam ? vcam.Lens.OrthographicSize : -1f;

    // 디버그 로그
    public bool debug;
    private void Log(string msg) { if (debug) Debug.Log($"[CameraFX] {msg}"); }

    private Coroutine _zoomCo;
    private Coroutine _shakeCo;

    // Perlin(있으면 사용)
    private Component _perlin; // CinemachineBasicMultiChannelPerlin

    private void Awake()
    {
        if (!vcam) vcam = GetComponent<CinemachineCamera>();

        // Perlin 찾아두기(없어도 OK)
        _perlin = GetComponentInChildren(typeof(CinemachineBasicMultiChannelPerlin), true);
        if (!_perlin && vcam) _perlin = vcam.GetComponent(typeof(CinemachineBasicMultiChannelPerlin));
    }

    private void Start()
    {
        if (readDefaultFromVcamOnStart && vcam != null)
            defaultOrthoSize = vcam.Lens.OrthographicSize;  // ✅ v3: VCam의 Lens 값을 기본값으로
    }

    public void PlayProfile(CameraFXProfile profile)
    {
        if (profile == null || vcam == null) return;

        if (profile.enableZoom)
        {
            if (_zoomCo != null && profile.resetZoomIfAlreadyZooming)
            {
                StopCoroutine(_zoomCo);
                _zoomCo = null;
                vcam.Lens.OrthographicSize = defaultOrthoSize;
            }
            if (_zoomCo == null) _zoomCo = StartCoroutine(ZoomSequence(profile));
        }

        if (profile.enableShake)
        {
            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(ShakeSequence(profile));
        }
    }

    private IEnumerator ZoomSequence(CameraFXProfile p)
    {
        float start = vcam.Lens.OrthographicSize;
        float end = Mathf.Max(0.01f, p.zoomToSize);

        // In
        float t = 0f;
        while (t < p.zoomInTime)
        {
            float u = p.zoomInTime > 0f ? t / p.zoomInTime : 1f;
            float k = p.easeIn.Evaluate(u);
            vcam.Lens.OrthographicSize = Mathf.Lerp(start, end, k);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        vcam.Lens.OrthographicSize = end;

        // Hold
        if (p.holdTime > 0f) yield return new WaitForSecondsRealtime(p.holdTime);

        // Out
        float startOut = vcam.Lens.OrthographicSize;
        float endOut = defaultOrthoSize;
        t = 0f;
        while (t < p.resetTime)
        {
            float u = p.resetTime > 0f ? t / p.resetTime : 1f;
            float k = p.easeOut.Evaluate(u);
            vcam.Lens.OrthographicSize = Mathf.Lerp(startOut, endOut, k);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        vcam.Lens.OrthographicSize = endOut;
        _zoomCo = null;
    }

    private IEnumerator ShakeSequence(CameraFXProfile p)
    {
        // 1) Impulse 우선 (v3: DefaultVelocity)
        if (impulseSource)
        {
            impulseSource.DefaultVelocity = Vector3.one * Mathf.Max(0f, p.shakeAmplitude);
            impulseSource.GenerateImpulse();
            if (p.shakeDuration > 0f) yield return new WaitForSecondsRealtime(p.shakeDuration);
            _shakeCo = null;
            yield break;
        }

        // 2) Perlin 직접 제어 (v2/v3 호환: 필드/프로퍼티 둘 다 시도)
        if (!_perlin)
        {
            _perlin = GetComponentInChildren(typeof(CinemachineBasicMultiChannelPerlin), true);
            if (!_perlin && vcam) _perlin = vcam.GetComponent(typeof(CinemachineBasicMultiChannelPerlin));
        }
        if (!_perlin) { _shakeCo = null; yield break; }

        float prevAmp = GetFloatFieldOrProp(_perlin, "m_AmplitudeGain", "AmplitudeGain");
        float prevFreq = GetFloatFieldOrProp(_perlin, "m_FrequencyGain", "FrequencyGain");

        SetFloatFieldOrProp(_perlin, "m_AmplitudeGain", "AmplitudeGain", p.shakeAmplitude);
        SetFloatFieldOrProp(_perlin, "m_FrequencyGain", "FrequencyGain", p.shakeFrequency);

        if (p.shakeDuration > 0f) yield return new WaitForSecondsRealtime(p.shakeDuration);

        SetFloatFieldOrProp(_perlin, "m_AmplitudeGain", "AmplitudeGain", 0f);
        SetFloatFieldOrProp(_perlin, "m_FrequencyGain", "FrequencyGain", prevFreq);

        _shakeCo = null;
    }

    // ===== 리플렉션 유틸 =====
    private static float GetFloatFieldOrProp(object obj, string fieldName, string propName)
    {
        if (obj == null) return 0f;
        var t = obj.GetType();
        var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(float)) return (float)f.GetValue(obj);
        var p = t.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(float) && p.CanRead) return (float)p.GetValue(obj);
        return 0f;
    }

    private static void SetFloatFieldOrProp(object obj, string fieldName, string propName, float value)
    {
        if (obj == null) return;
        var t = obj.GetType();
        var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(float)) { f.SetValue(obj, value); return; }
        var p = t.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(float) && p.CanWrite) { p.SetValue(obj, value); return; }
    }

    // 강제 리셋 (원하면 사용)
    public void ResetZoom(float time = 0.2f, AnimationCurve ease = null)
    {
        if (_zoomCo != null) StopCoroutine(_zoomCo);
        _zoomCo = StartCoroutine(ResetZoomCo(time, ease));
    }

    private IEnumerator ResetZoomCo(float time, AnimationCurve ease)
    {
        if (ease == null) ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
        float start = vcam.Lens.OrthographicSize;
        float end = defaultOrthoSize;
        float t = 0f;
        while (t < time)
        {
            float u = time > 0f ? t / time : 1f;
            float k = ease.Evaluate(u);
            vcam.Lens.OrthographicSize = Mathf.Lerp(start, end, k);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        vcam.Lens.OrthographicSize = end;
        _zoomCo = null;
    }
}
