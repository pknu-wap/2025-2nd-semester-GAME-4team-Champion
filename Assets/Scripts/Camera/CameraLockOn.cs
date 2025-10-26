using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine; // v3

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public class CameraLockOn : MonoBehaviour
{
    [Header("Cinemachine (v3)")]
    [SerializeField] private CinemachineCamera vcam;

    [Header("Targets")]
    public Transform player;
    [SerializeField] private Transform anchor; // 비워두면 자동 생성

    [Header("Enemy Scan")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float scanRadius = 30f;
    [SerializeField] private float rescanInterval = 0.15f;

    [Header("Zoom (VCam Lens)")]
    [SerializeField] private float baseOrthoSize = 3.6f;
    [SerializeField] private float minOrthoSize = 3.2f;
    [SerializeField] private float maxOrthoSize = 6.0f;
    [SerializeField] private float minDistance = 2.0f;
    [SerializeField] private float maxDistance = 12.0f;
    [SerializeField] private float zoomSmooth = 0.15f;

    [Header("Follow Smoothing")]
    [SerializeField] private float followLerp = 12f;

    [Header("Combat Gating")]
    [SerializeField] private bool onlyWhenInCombat = true;
    [SerializeField] private string combatEnterTag = "Tag.Combat.Enter";
    [SerializeField] private string combatExitTag = "Tag.Combat.Exit";

    [Header("External Zoom (Tag.Zoom)")]
    [Tooltip("외부 줌 연출 태그. 수신 시 락온의 줌 제어를 일시 정지합니다.")]
    [SerializeField] private string externalZoomTag = "Tag.Zoom";
    [Tooltip("Tag.Zoom 수신 시 락온 줌을 억제할 시간(초). 외부 카메라 연출 길이와 맞추세요.")]
    [SerializeField] private float externalZoomHold = 0.45f;
    [Tooltip("줌 억제 중 앵커(중점) 이동은 계속할지 여부")]
    [SerializeField] private bool moveAnchorWhileSuppressed = true;

    // 외부 제어 API
    public void SetCombatState(bool active) => _inCombat = active;
    public void EnterCombat() => SetCombatState(true);
    public void ExitCombat() => SetCombatState(false);

    // 내부 상태
    private bool _inCombat = false;
    private float _rescanTimer;
    private float _zoomVel;
    private Transform _lockedEnemy;
    private Transform _originalFollow;
    private float _originalOrtho;

    // 줌 억제(외부 연출 보호)
    private float _zoomSuppressUntil = -1f;
    public void SuppressLockOnZoom(float duration)
    {
        _zoomSuppressUntil = Mathf.Max(_zoomSuppressUntil, Time.unscaledTime + Mathf.Max(0f, duration));
    }
    private bool IsZoomSuppressed => Time.unscaledTime < _zoomSuppressUntil;

    // 스캔 버퍼/중복제거
    private static readonly Collider2D[] sBuf = new Collider2D[16];
    private readonly HashSet<Transform> _uniqueRoots = new HashSet<Transform>();

    private void Awake()
    {
        if (!vcam) vcam = FindFirstObjectByType<CinemachineCamera>();
        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (!anchor)
        {
            var go = new GameObject("LockOnAnchor");
            go.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            anchor = go.transform;
        }

        CacheAndBindVcam();

        // 태그 구독 (전투 + 외부줌)
        TagBus.OnTag += OnTag;
    }

    private void OnEnable()
    {
        CacheAndBindVcam();
        // 기본은 비전투
        _inCombat = false;
    }

    private void OnDisable()
    {
        if (vcam)
        {
            vcam.Follow = _originalFollow;
            vcam.Lens.OrthographicSize = _originalOrtho;
        }
        _lockedEnemy = null;
        TagBus.OnTag -= OnTag;
    }

    private void CacheAndBindVcam()
    {
        if (!vcam) return;
        if (_originalFollow == null) _originalFollow = vcam.Follow;
        _originalOrtho = vcam.Lens.OrthographicSize;

        if (baseOrthoSize > 0f) vcam.Lens.OrthographicSize = baseOrthoSize;
        else baseOrthoSize = _originalOrtho;

        if (anchor) vcam.Follow = anchor;
    }

    private void OnTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return;

        if (tag == combatEnterTag) _inCombat = true;
        else if (tag == combatExitTag) _inCombat = false;
        else if (tag == externalZoomTag)
        {
            // 외부 줌 연출 보호
            SuppressLockOnZoom(externalZoomHold);
        }
    }

    private void Update()
    {
        if (!vcam || !player || !anchor) return;

        // 전투 중 아니면: 스캔/줌 다 비활성, 기본값 유지
        if (onlyWhenInCombat && !_inCombat)
        {
            RecoverToBase();
            return;
        }

        // 주기적 스캔(정확히 1명 루트만)
        _rescanTimer -= Time.unscaledDeltaTime;
        if (_rescanTimer <= 0f)
        {
            _rescanTimer = rescanInterval;
            ScanTarget();
        }

        bool lockActive = _lockedEnemy != null;

        // 앵커 이동 (억제 옵션 상관없이 계속할지 선택)
        if (lockActive)
        {
            Vector3 a = player.position;
            Vector3 b = _lockedEnemy.position;
            Vector3 mid = (a + b) * 0.5f;

            if (moveAnchorWhileSuppressed || !IsZoomSuppressed)
            {
                anchor.position = Vector3.Lerp(
                    anchor.position,
                    mid,
                    1f - Mathf.Exp(-followLerp * Time.unscaledDeltaTime)
                );
            }
        }
        else
        {
            if (moveAnchorWhileSuppressed || !IsZoomSuppressed)
            {
                anchor.position = Vector3.Lerp(
                    anchor.position,
                    player.position,
                    1f - Mathf.Exp(-followLerp * Time.unscaledDeltaTime)
                );
            }
        }

        // 👉 외부 줌 연출 보호: 억제 중엔 절대 Lens 사이즈를 건드리지 않음
        if (IsZoomSuppressed) return;

        // 락온 아니면 기본으로 복귀
        if (!lockActive)
        {
            float target = baseOrthoSize;
            float next = Mathf.SmoothDamp(vcam.Lens.OrthographicSize, target, ref _zoomVel, zoomSmooth);
            vcam.Lens.OrthographicSize = next;
            return;
        }

        // 락온 줌(거리 기반)
        Vector3 p = player.position;
        Vector3 e = _lockedEnemy.position;
        float dist = Vector2.Distance(p, e);
        float t = Mathf.InverseLerp(minDistance, maxDistance, dist);
        float targetSize = Mathf.Lerp(minOrthoSize, maxOrthoSize, t);
        float size = Mathf.SmoothDamp(vcam.Lens.OrthographicSize, targetSize, ref _zoomVel, zoomSmooth);
        vcam.Lens.OrthographicSize = size;
    }

    private void RecoverToBase()
    {
        // 줌 억제 중이면 사이즈 복구도 하지 않음(외부 연출을 존중)
        if (!IsZoomSuppressed)
        {
            float target = baseOrthoSize;
            float next = Mathf.SmoothDamp(vcam.Lens.OrthographicSize, target, ref _zoomVel, zoomSmooth);
            vcam.Lens.OrthographicSize = next;
        }

        if (moveAnchorWhileSuppressed || !IsZoomSuppressed)
        {
            anchor.position = Vector3.Lerp(
                anchor.position,
                player.position,
                1f - Mathf.Exp(-followLerp * Time.unscaledDeltaTime)
            );
        }

        _lockedEnemy = null;
    }

    private void ScanTarget()
    {
        _lockedEnemy = null;
        if (!player) return;

        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(enemyMask);

        int count = Physics2D.OverlapCircle((Vector2)player.position, scanRadius, filter, sBuf);

        _uniqueRoots.Clear();
        for (int i = 0; i < count; i++)
        {
            var col = sBuf[i];
            if (!col) continue;
            _uniqueRoots.Add(col.transform.root);
        }

        if (_uniqueRoots.Count == 1)
        {
            foreach (var t in _uniqueRoots) { _lockedEnemy = t; break; }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (player)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(player.position, scanRadius);
        }
    }
#endif
}
