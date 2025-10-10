using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ª�� �� �׼�(���� �Һ���).
/// - ����ȭ: Ű ��� �̵���("HEAL"), �ڷ�ƾ ����
/// - �±�: ����/����
/// </summary>
public class Player_Heal : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private float healDuration = 0.5f;
    [SerializeField] private float healAmount = 35f;

    [Header("Charges")]
    [SerializeField] private int maxCharges = 2;
    [SerializeField] private bool refillOnEnable = false;
    private int chargesLeft;

    [Header("Refs")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    private PlayerMove inputWrapper;
    private InputAction healAction;
    private const string LOCK_HEAL = "HEAL";

    private Coroutine healCo;
    public bool IsHealing { get; private set; }
    public int ChargesLeft => chargesLeft;
    public int MaxCharges => maxCharges;

    // ===== Tag system =====
    public const string TAG_HEAL_START = "Tag.Heal.Start";
    public const string TAG_HEAL_END = "Tag.Heal.End";
    public event System.Action<string> OnTag;

    public void Bind(PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        combat = c; moveRef = m; animator = a;
    }

    private void Reset()
    {
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        inputWrapper = new PlayerMove();
        chargesLeft = Mathf.Clamp(maxCharges, 0, Mathf.Max(0, maxCharges));
    }

    private void OnEnable()
    {
        inputWrapper.Enable();
        inputWrapper.Combat.Enable();

        if (refillOnEnable)
            ResetHealCharges();

        healAction = inputWrapper.asset.FindAction("Heal");
        if (healAction != null)
            healAction.performed += OnHealPerformed;
        else
            Debug.LogWarning("[Player_Heal] 'Heal' �׼��� �����ϴ�.");
    }

    private void OnDisable()
    {
        if (healAction != null)
            healAction.performed -= OnHealPerformed;

        inputWrapper.Disable();

        if (healCo != null) StopCoroutine(healCo);
        healCo = null;

        if (IsHealing)
        {
            // ���� ���� �±�
            OnTag?.Invoke(TAG_HEAL_END);
        }

        IsHealing = false;
        moveRef?.RemoveMovementLock(LOCK_HEAL, false);
        if (animator) animator.SetBool("Healing", false);
    }

    private void OnHealPerformed(InputAction.CallbackContext _)
    {
        if (IsHealing) return;
        if (chargesLeft <= 0) { Debug.Log("[HEAL] No charges left."); return; }
        StartHealing();
    }

    public void StartHealing()
    {
        if (IsHealing) return;
        if (chargesLeft <= 0) return;

        chargesLeft = Mathf.Max(0, chargesLeft - 1);
        healCo = StartCoroutine(HealRoutine());
    }

    /// <summary>�� ���� ���. refundCharge=true �� ���� 1ȸ ��ȯ.</summary>
    public void CancelHealing(bool refundCharge = false)
    {
        if (!IsHealing) return;

        if (refundCharge)
            chargesLeft = Mathf.Min(maxCharges, chargesLeft + 1);

        if (healCo != null) StopCoroutine(healCo);
        healCo = null;

        IsHealing = false;
        moveRef?.RemoveMovementLock(LOCK_HEAL, false);
        if (animator) animator.SetBool("Healing", false);

        // �±�: ����
        OnTag?.Invoke(TAG_HEAL_END);
    }

    public void ResetHealCharges()
    {
        chargesLeft = Mathf.Clamp(maxCharges, 0, Mathf.Max(0, maxCharges));
        Debug.Log($"[HEAL] Charges reset: {chargesLeft}/{maxCharges}");
    }

    private IEnumerator HealRoutine()
    {
        IsHealing = true;
        OnTag?.Invoke(TAG_HEAL_START); // �±�: ����

        combat?.BlockStaminaRegenFor(healDuration);
        combat?.StartActionLock(healDuration, true);

        moveRef?.AddMovementLock(LOCK_HEAL, false, true);
        if (animator) animator.SetBool("Healing", true);

        yield return new WaitForSeconds(healDuration);

        if (combat != null) combat.Heal(+combat.HPMax * healAmount);

        IsHealing = false;
        moveRef?.RemoveMovementLock(LOCK_HEAL, false);
        if (animator) animator.SetBool("Healing", false);
        healCo = null;

        OnTag?.Invoke(TAG_HEAL_END); // �±�: ����
    }
}
