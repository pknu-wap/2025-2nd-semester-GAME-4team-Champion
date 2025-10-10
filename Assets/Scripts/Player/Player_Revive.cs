using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ��� �� ���� �ð� ���� Attack ��Ÿ���� ����� ���� ü������ ��Ȱ.
/// - �ڷ�ƾ ����ȭ: try/finally�� ��/�Է�/���� Ȯ���� ����
/// - �̵���: Ű ���("REVIVE")
/// - �±�: Mash(Ŭ�� ��), Success/Fail
/// </summary>
public class Player_Revive : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerHit hit; // ���� �� ���� OFF ��ȯ��

    [Header("Revive (Mashing) Settings")]
    [SerializeField] private int maxRevives = 2;
    [SerializeField] private float reviveWindowSec = 3f;
    [SerializeField, Range(0f, 1f)] private float maxHealPercent = 0.3f;
    [SerializeField] private int pressesForMax = 30;
    [SerializeField] private string attackActionName = "Attack";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    // ===== Tag system =====
    public const string TAG_REVIVE_MASH = "Tag.Revive.Mash";     // ��: ���� Ŭ�� �� (OnTagInt)
    public const string TAG_REVIVE_SUCCESS = "Tag.Revive.Success";  // ���� ��
    public const string TAG_REVIVE_FAIL = "Tag.Revive.Fail";     // ���� ��
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

    /// <summary>��� �� PlayerCombat.OnDeath()���� ȣ��</summary>
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

        // �Է� ����
        inputWrapper.Enable();
        attackAction = inputWrapper.asset.FindAction(attackActionName);
        if (attackAction != null) attackAction.started += OnMash;
        else if (debugLogs) Debug.LogWarning($"[Revive] '{attackActionName}' �׼� ����.");

        // �̵��� + ���� �Է� ��Ȱ��
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
            // â ����
            while (Time.time < windowEnd && combat != null && combat.HP <= 0f)
                yield return null;

            // ȸ�� ���
            float ratio = (pressesForMax > 0) ? Mathf.Clamp01((float)mashCount / pressesForMax) : 1f;
            float healAmount = (combat != null ? combat.HPMax : 0f) * maxHealPercent * ratio;

            if (debugLogs) Debug.Log($"[Revive] presses={mashCount}, ratio={ratio:F2}, heal={healAmount:F1}");

            if (combat != null && healAmount > 0f)
            {
                // ��Ȱ!
                combat.Heal(healAmount);
                success = true;

                // ���� ���� OFF
                hit?.SetDeadInvulnerable(false);

                // ���/���� ����
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

                // �±�: ����
                OnTag?.Invoke(TAG_REVIVE_SUCCESS);
            }
            else
            {
                // �±�: ����
                OnTag?.Invoke(TAG_REVIVE_FAIL);
            }
        }
        finally
        {
            // �Է� ����
            if (attackAction != null) attackAction.started -= OnMash;
            inputWrapper.Disable();

            // �츮 Ű�� �̵��� ����(�����̸� �̹� hardFreeze ����)
            moveRef?.RemoveMovementLock(LOCK_REVIVE, hardFreezePhysics: !success);

            reviveActive = false;
        }
    }

    private void OnMash(InputAction.CallbackContext _)
    {
        // ���� & �±�(�� ����)
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
