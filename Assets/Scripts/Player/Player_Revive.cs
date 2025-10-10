using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 사망 시 일정 시간 동안 Attack 연타량에 비례해 일정 체력으로 부활.
/// - 코루틴 안정화: try/finally로 락/입력/상태 확실히 정리
/// - 이동락: 키 기반("REVIVE")
/// - 태그: Mash(클릭 수), Success/Fail
/// </summary>
public class Player_Revive : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerHit hit; // 죽을 때 무적 OFF 전환용

    [Header("Revive (Mashing) Settings")]
    [SerializeField] private int maxRevives = 2;
    [SerializeField] private float reviveWindowSec = 3f;
    [SerializeField, Range(0f, 1f)] private float maxHealPercent = 0.3f;
    [SerializeField] private int pressesForMax = 30;
    [SerializeField] private string attackActionName = "Attack";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    // ===== Tag system =====
    public const string TAG_REVIVE_MASH = "Tag.Revive.Mash";     // 값: 누적 클릭 수 (OnTagInt)
    public const string TAG_REVIVE_SUCCESS = "Tag.Revive.Success";  // 성공 시
    public const string TAG_REVIVE_FAIL = "Tag.Revive.Fail";     // 실패 시
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

    // Lock key
    private const string LOCK_REVIVE = "REVIVE";

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

        // 입력 세팅
        inputWrapper.Enable();
        attackAction = inputWrapper.asset.FindAction(attackActionName);
        if (attackAction != null) attackAction.started += OnMash;
        else if (debugLogs) Debug.LogWarning($"[Revive] '{attackActionName}' 액션 없음.");

        // 이동락 + 전투 입력 비활성
        moveRef?.AddMovementLock(LOCK_REVIVE, hardFreezePhysics: false, zeroVelocity: true);

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

                // 잠금/상태 복원
                moveRef?.RemoveMovementLock(LOCK_REVIVE, hardFreezePhysics: true);
                if (atkHub) atkHub.enabled = atkHubWas;
                if (nAtk) nAtk.enabled = nAtkWas;
                if (cgAtk) cgAtk.enabled = cgAtkWas;
                if (cAtk) cAtk.enabled = cAtkWas;
                if (defense) defense.enabled = defWas;

                animator?.ResetTrigger("Die");
                animator?.SetBool("isBlocking", false);
                moveRef?.SetGuardSpeedScale(1f);
                animator?.SetTrigger("Revive");

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

            // 우리 키의 이동락 해제(성공이면 이미 hardFreeze 해제)
            moveRef?.RemoveMovementLock(LOCK_REVIVE, hardFreezePhysics: !success);

            reviveActive = false;
        }
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
        moveRef?.RemoveMovementLock(LOCK_REVIVE, hardFreezePhysics: false);
        reviveActive = false;
    }
}
