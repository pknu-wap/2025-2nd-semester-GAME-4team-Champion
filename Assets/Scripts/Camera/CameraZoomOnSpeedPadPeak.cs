using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Tag.SpeedPad.Peak 수신 시 holdDuration 동안 '가산 줌'을 유지.
/// 값 적용은 OrthoSizeAddExtension에 위임하여 파이프라인 마지막에서 보장.
/// 외부 스킬 연출(Tag.Zoom) 억제 중이라도 피크가 진행 중이면 가산 유지.
/// </summary>
[DefaultExecutionOrder(200)]
public class CameraZoomOnSpeedPadPeak : MonoBehaviour
{
    [Header("Cinemachine (auto-find)")]
    [SerializeField] private CinemachineCamera vcam;

    [Header("Zoom On Peak")]
    [SerializeField] private float peakZoomOutDelta = 2.0f;
    [SerializeField] private float holdDuration = 0.7f;
    [SerializeField] private float zoomSmooth = 0.12f;

    [Header("VFX (One-Shot)")]
    [SerializeField] private GameObject oneShotVfxPrefab;
    [SerializeField] private Vector2 vfxOffset = Vector2.zero;
    [SerializeField] private bool flipWithLocalScaleX = true;

    [Header("Tags")]
    [SerializeField] private string peakTag = "Tag.SpeedPad.Peak";
    [SerializeField] private string externalZoomTag = "Tag.Zoom";
    [SerializeField] private float externalZoomHold = 0.45f;

    private float _suppressUntil = -1f;  // 외부 줌 억제
    private float _peakUntil = -1f;      // 피크 홀드 종료 시각

    private float _currentAdd, _targetAdd, _vel;
    private OrthoSizeAddExtension _ext;
    private Transform _self;

    private void Awake()
    {
        if (!vcam) vcam = FindFirstObjectByType<CinemachineCamera>();
        _self = transform;
        EnsureExtension();
        TagBus.OnTag += OnTag;
    }

    private void OnDestroy()
    {
        TagBus.OnTag -= OnTag;
        if (_ext) _ext.add = 0f;
    }

    private void OnDisable()
    {
        if (_ext) _ext.add = 0f;
        _currentAdd = _targetAdd = 0f;
    }

    private void EnsureExtension()
    {
        if (!vcam) return;
        _ext = vcam.GetComponent<OrthoSizeAddExtension>();
        if (!_ext) _ext = vcam.gameObject.AddComponent<OrthoSizeAddExtension>();
        _ext.add = 0f;
    }

    private void OnTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;

        if (tag == peakTag)
        {
            _peakUntil = Time.unscaledTime + Mathf.Max(0f, holdDuration);
            _targetAdd = peakZoomOutDelta;

            // VFX 1회
            if (oneShotVfxPrefab && _self)
            {
                var go = Instantiate(oneShotVfxPrefab);
                go.transform.position = (Vector2)_self.position + vfxOffset;

                if (flipWithLocalScaleX)
                {
                    float sign = Mathf.Sign(_self.localScale.x == 0 ? 1f : _self.localScale.x);
                    var s = go.transform.localScale; s.x = Mathf.Abs(s.x) * sign;
                    go.transform.localScale = s;
                }

                var ps = go.GetComponent<ParticleSystem>();
                if (ps)
                {
                    var main = ps.main;
                    float life = main.duration + (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                        ? main.startLifetime.constantMax : main.startLifetime.constant);
                    Destroy(go, life + 0.5f);
                }
                else Destroy(go, 5f);
            }
        }
        else if (tag == externalZoomTag)
        {
            _suppressUntil = Mathf.Max(_suppressUntil, Time.unscaledTime + Mathf.Max(0f, externalZoomHold));
        }
    }

    private void LateUpdate()
    {
        if (!vcam || !_ext) return;

        bool peakActive = Time.unscaledTime < _peakUntil;
        bool suppressed = Time.unscaledTime < _suppressUntil;

        // 억제 중 + 피크 미진행이면 가산 0
        if (suppressed && !peakActive)
        {
            _targetAdd = 0f;
        }
        else
        {
            _targetAdd = peakActive ? peakZoomOutDelta : 0f;
        }

        _currentAdd = Mathf.SmoothDamp(_currentAdd, _targetAdd, ref _vel, zoomSmooth);

        // ❗ 파이프라인 마지막에서 확장이 최종적으로 더해줌
        _ext.add = _currentAdd;
    }
}
