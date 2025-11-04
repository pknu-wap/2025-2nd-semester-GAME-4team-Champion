<<<<<<< HEAD
﻿using UnityEngine;
=======
using UnityEngine;
>>>>>>> Enemy

/// <summary>
/// SpeedPad 존에 들어가면 이동 배수(currentScale)를 관리.
/// - Q 연타 시마다 mashStep 만큼 증가(GetKeyDown)
/// - autoRampPerSec(선택)로 초당 자연 가속 가능(0이면 꺼짐)
/// - 최댓값(settings.maxScale) 클램프
/// - SetGuardSpeedScale(currentScale) 적용 직후, '처음' maxScale 도달 프레임에만 Tag.SpeedPad.Peak 발행
/// - 존을 벗어나면 exitEaseTime에 맞춰 1배로 복귀, 충분히 낮아지면 재무장
/// </summary>
[DisallowMultipleComponent]
public class SpeedPadEffect : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMoveBehaviour playerMove; // 배수 적용 대상(필수)
    [SerializeField] private SpeedPadSettings settings;      // maxScale, exitEaseTime 사용

    [Header("Peak Tag")]
    [SerializeField] private string peakTag = "Tag.SpeedPad.Peak";
    [Tooltip("이 값 이하로 내려가면 다음 피크 때 다시 발행(재무장)")]
    [Range(0.5f, 1f)][SerializeField] private float rearmThreshold = 0.98f;
    [SerializeField] private float epsilon = 0.0001f;

    [Header("In-Zone Scale Control")]
    [Tooltip("연타(키다운 1번당) 증가량")]
    [SerializeField] private float mashStep = 0.15f;
    [Tooltip("연타 키")]
    [SerializeField] private KeyCode mashKey = KeyCode.Q;
    [Tooltip("자연 가속(초당). 0이면 꺼짐")]
    [SerializeField] private float autoRampPerSec = 0f;
    [Tooltip("존 안에서 자연 감속(초당). 0이면 꺼짐")]
    [SerializeField] private float inZoneDecayPerSec = 0f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    // 내부 상태
    private bool inZone = false;
    private bool peakFired = false;
    private float currentScale = 1f;
    private float exitEaseT = 0f;

    private void Reset()
    {
        if (!playerMove) playerMove = GetComponentInParent<PlayerMoveBehaviour>();
    }

    private void Awake()
    {
        if (!playerMove) playerMove = GetComponentInParent<PlayerMoveBehaviour>();
        currentScale = 1f;
        ApplyScale(); // 1배 적용
    }

    private void OnValidate()
    {
        if (settings && settings.maxScale < 1f)
        {
            settings.maxScale = 1f;
            if (debugLog) Debug.LogWarning("[SpeedPadEffect] settings.maxScale < 1 → 1로 보정", this);
        }
        rearmThreshold = Mathf.Clamp(rearmThreshold, 0.5f, 1f);
    }

    private void Update()
    {
        if (!settings)
        {
            if (debugLog) Debug.LogWarning("[SpeedPadEffect] SpeedPadSettings 미지정", this);
            return;
        }

        float maxScale = Mathf.Max(1f, settings.maxScale);

        if (inZone)
        {
            // 1) Q 연타 가속 (키다운 프레임마다 1회)
            if (mashStep > 0f && Input.GetKeyDown(mashKey))
                currentScale += mashStep;

            // 2) 자연 가속(선택)
            if (autoRampPerSec > 0f)
                currentScale += autoRampPerSec * Time.deltaTime;

            // 3) 존 안 자연 감속(선택)
            if (inZoneDecayPerSec > 0f && currentScale > 1f)
                currentScale = Mathf.MoveTowards(currentScale, 1f, inZoneDecayPerSec * Time.deltaTime);

            // 4) 클램프
            currentScale = Mathf.Min(currentScale, maxScale);

            // 5) 적용 + 피크 감지
            ApplyScale();

            // 이탈 복귀 타이머 초기화
            exitEaseT = 0f;
        }
        else
        {
            // 존 밖: exitEaseTime에 맞춰 1배 복귀
            if (currentScale > 1f)
            {
                exitEaseT += Time.deltaTime;
                float t = settings.exitEaseTime > 0f ? Mathf.Clamp01(exitEaseT / settings.exitEaseTime) : 1f;
                currentScale = Mathf.Lerp(currentScale, 1f, t);
                if (currentScale <= 1f + epsilon) currentScale = 1f;
                ApplyScale();
            }

            // 충분히 낮아지면 재무장
            if (peakFired && currentScale <= rearmThreshold)
                peakFired = false;
        }
    }

    /// <summary>SpeedPadZone에서 호출: 존 진입</summary>
    public void OnEnterPad(SpeedPadSettings padSettings)
    {
        if (padSettings != null) settings = padSettings;
        inZone = true;

        float maxScale = Mathf.Max(1f, settings ? settings.maxScale : 1f);
        if (currentScale < maxScale - epsilon) peakFired = false;

        if (debugLog) Debug.Log("[SpeedPadEffect] Enter Pad", this);
    }

    /// <summary>SpeedPadZone에서 호출: 존 이탈</summary>
    public void OnExitPad()
    {
        inZone = false;
        if (debugLog) Debug.Log("[SpeedPadEffect] Exit Pad", this);
    }

    // ---- 내부 유틸 ----

    private void ApplyScale()
    {
        if (!playerMove)
        {
            if (debugLog) Debug.LogWarning("[SpeedPadEffect] PlayerMoveBehaviour 미지정", this);
            return;
        }

        // 1) 실제 이동 배수 적용
        playerMove.SetGuardSpeedScale(currentScale);

        // 2) 적용 직후 피크 감지(정확한 타이밍)
        NotifyScale(currentScale);
    }

    private void NotifyScale(float scale)
    {
        if (!settings) return;

        float maxScale = Mathf.Max(1f, settings.maxScale);

        // 재무장
        if (peakFired && scale <= rearmThreshold)
            peakFired = false;

        // 피크 도달 1회
        if (inZone && !peakFired && (maxScale - scale) <= epsilon)
        {
            peakFired = true;
            currentScale = maxScale; // 드리프트 방지 스냅
            if (debugLog) Debug.Log("[SpeedPadEffect] PEAK → Tag 발행", this);
            TagBus.Raise(peakTag);
        }
    }
}
