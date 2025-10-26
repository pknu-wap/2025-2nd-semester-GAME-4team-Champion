using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 사망 시 일정 시간 동안 Attack 연타량에 비례해 일정 체력으로 부활.
/// - try/finally로 락/입력/상태 확실히 정리
/// - 이동락: 키 기반("REVIVE", "REVIVE_SUCCESS")
/// - 태그: Mash(클릭 수), Success/Fail
/// </summary>
public class Player_Revive : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerHit hit; // 죽음 무적 OFF 전환용

    [Header("Revive (Mashing) Settings")]
    [SerializeField] private int maxRevives = 2;
    [SerializeField] private float reviveWindowSec = 3f;
    [SerializeField, Range(0f, 1f)] private float maxHealPercent = 0.3f;
    [SerializeField] private int pressesForMax = 30;
    [SerializeField] private string attackActionName = "Attack";

    [Header("Movement Lock During Revive (Casting)")]
    [SerializeField] private bool lockMovementHardDuringRevive = true; // 창 동안 완전 고정(물리 Freeze)
    [SerializeField] private bool blockFlipDuringRevive = true;

    [Header("Movement Lock After Success")]
    [SerializeField] private bool lockMovementOnSuccess = true;     // ✅ 성공 직후에도 잠금
    [SerializeField] private float successLockDuration = 0.6f;      // ✅ 잠금 유지 시간
    [SerializeField] private bool hardFreezeOnSuccess = true;       // ✅ 물리 Freeze 포함 여부
    [SerializeField] private bool blockFlipOnSuccess = true;        // ✅ 성공 락 동안 Flip 차단

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    // ===== Tag system =====
    public const string TAG_REVIVE_MASH = "Tag.Revive.Mash";     // 값: 누적 클릭 수 (OnTagInt)
    public const string TAG_REVIVE_SUCCESS = "Tag.Revive.Success";
    public const string TAG_REVIVE_FAIL = "Tag.Revive.Fail";
    public event System.Action<string> OnTag;
    public event System.Action<string, int> OnTagInt;

    // state
    private int usedRevives = 0;
    private bool reviveActive = false;
    private int mashCount = 0;
    private float windowEnd = 0f;

    // Input
    private PlayerMove inputWrapper;
    private InputAction attackAction;

    // Lock keys
    private const string LOCK_REVIVE = "REVIVE";
    private const string LOCK_REVIVE_SUCCESS = "REVIVE_SUCCESS";

    public bool IsReviving => reviveActive;
    public int MashCount => mashCount;

    private void Reset()
    {
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!hit) hit = GetComponent<PlayerHit>();
    }

    private void Awake()
    {
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!hit) hit = GetComponent<PlayerHit>();
        inputWrapper = new PlayerMove();
    }

    /// <summary>사망 시 PlayerCombat.OnDeath()에서 호출</summary>
    public bool BeginReviveIfAvailable()
    {
        if (reviveActive) return false;
        if (usedRevives >= maxRevives) return false;
        if (combat == null || combat.HP > 0f) return false;

        usedRevives++;
        StartCoroutine(ReviveWindow());
        return true;
    }

    public void ResetRevives() => usedRevives = 0;

    private IEnumerator ReviveWindow()
    {
        reviveActive = true;
        mashCount = 0;
        windowEnd = Time.time + reviveWindowSec;

        // 입력 세팅 (연타 감지 전용)
        inputWrapper.Enable();
        attackAction = inputWrapper.asset.FindAction(attackActionName);
        if (attackAction != null) attackAction.started += OnMash;
        else if (debugLogs) Debug.LogWarning($"[Revive] '{attackActionName}' 액션 없음.");

        // 🔒 이동락 + 속도 0 + (옵션) 물리 동결 + (옵션) 플립 차단
        moveRef?.AddMovementLock(
            LOCK_REVIVE,
            hardFreezePhysics: lockMovementHardDuringRevive,
            zeroVelocity: true
        );
        if (blockFlipDuringRevive) moveRef?.SetFlipFromMovementBlocked(true);

        // 전투 입력 비활성
        var atkHub = GetComponent<PlayerAttack>();
        var nAtk = GetComponent<N_ATK>();
        var cgAtk = GetComponent<CG_ATK>();
        var cAtk = GetComponent<C_ATK>();
        var defense = GetComponent<PlayerDefense>();

        bool atkHubWas = atkHub ? atkHub.enabled : false;
        bool nAtkWas = nAtk ? nAtk.enabled : false;
        bool cgAtkWas = cgAtk ? cgAtk.enabled : false;
        bool cAtkWas = cAtk ? cAtk.enabled : false;
        bool defWas = defense ? defense.enabled : false;

        if (atkHub) atkHub.enabled = false;
        if (nAtk) nAtk.enabled = false;
        if (cgAtk) cgAtk.enabled = false;
        if (cAtk) cAtk.enabled = false;
        if (defense) defense.enabled = false;

        if (defense)
        {
            defense.ForceBlockFor(0f);
            animator?.SetBool("isBlocking", false);
        }
        moveRef?.SetGuardSpeedScale(1f);

        bool success = false;

        try
        {
            // 창 유지
            while (Time.time < windowEnd && combat != null && combat.HP <= 0f)
                yield return null;

            // 회복 계산
            float ratio = (pressesForMax > 0) ? Mathf.Clamp01((float)mashCount / pressesForMax) : 1f;
            float healAmount = (combat != null ? combat.HPMax : 0f) * maxHealPercent * ratio;

            if (debugLogs) Debug.Log($"[Revive] presses={mashCount}, ratio={ratio:F2}, heal={healAmount:F1}");

            if (combat != null && healAmount > 0f)
            {
                // 부활!
                combat.Heal(healAmount);
                success = true;

                // 죽음 무적 OFF
                hit?.SetDeadInvulnerable(false);

                // 상태 복원
                if (atkHub) atkHub.enabled = atkHubWas;
                if (nAtk) nAtk.enabled = nAtkWas;
                if (cgAtk) cgAtk.enabled = cgAtkWas;
                if (cAtk) cAtk.enabled = cAtkWas;
                if (defense) defense.enabled = defWas;

                animator?.ResetTrigger("Die");
                animator?.SetBool("isBlocking", false);
                moveRef?.SetGuardSpeedScale(1f);
                animator?.SetTrigger("Revive");

                // ✅ 성공 직후에도 이동 잠금 유지(옵션)
                if (lockMovementOnSuccess && moveRef != null)
                {
                    moveRef.AddMovementLock(LOCK_REVIVE_SUCCESS, hardFreezeOnSuccess, true);
                    if (blockFlipOnSuccess) moveRef.SetFlipFromMovementBlocked(true);
                    StartCoroutine(ReleaseSuccessLockAfterDelay(successLockDuration));
                }

                // 태그: 성공
                OnTag?.Invoke(TAG_REVIVE_SUCCESS);
            }
            else
            {
                // 태그: 실패
                OnTag?.Invoke(TAG_REVIVE_FAIL);
            }
        }
        finally
        {
            // 입력 정리
            if (attackAction != null) attackAction.started -= OnMash;
            inputWrapper.Disable();

            // 🔓 캐스팅 락 해제 (성공 락은 별도로 남아 있을 수 있음)
            if (blockFlipDuringRevive) moveRef?.SetFlipFromMovementBlocked(false);
            moveRef?.RemoveMovementLock(LOCK_REVIVE, hardFreezePhysics: false);

            reviveActive = false;
        }
    }

    private IEnumerator ReleaseSuccessLockAfterDelay(float t)
    {
        if (t > 0f) yield return new WaitForSeconds(t);
        if (blockFlipOnSuccess) moveRef?.SetFlipFromMovementBlocked(false);
        moveRef?.RemoveMovementLock(LOCK_REVIVE_SUCCESS, hardFreezePhysics: hardFreezeOnSuccess);
    }

    private void OnMash(InputAction.CallbackContext _)
    {
        // 누적 & 태그(값 포함)
        mashCount++;
        OnTagInt?.Invoke(TAG_REVIVE_MASH, mashCount);

        if (debugLogs && (mashCount % 5 == 0))
            Debug.Log($"[Revive] mash x{mashCount}");
    }

    private void OnDisable()
    {
        if (reviveActive)
        {
            if (attackAction != null) attackAction.started -= OnMash;
            inputWrapper.Disable();
        }
        // 모든 락 안전 해제
        if (blockFlipDuringRevive || blockFlipOnSuccess) moveRef?.SetFlipFromMovementBlocked(false);
        moveRef?.RemoveMovementLock(LOCK_REVIVE, hardFreezePhysics: false);
        moveRef?.RemoveMovementLock(LOCK_REVIVE_SUCCESS, hardFreezePhysics: hardFreezeOnSuccess);
        reviveActive = false;
    }
}
