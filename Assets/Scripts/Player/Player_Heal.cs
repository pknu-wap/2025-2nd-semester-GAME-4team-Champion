using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

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

    // 🔸 Heal 성공 VFX
    [Header("VFX (On Success)")]
    [SerializeField] private GameObject healSuccessVFX;
    [SerializeField] private bool healVfxAttachToPlayer = true;
    [SerializeField] private Vector2 healVfxOffset = new Vector2(0f, 0f);
    [SerializeField] private float healVfxLifetime = 1.2f;

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
            Debug.LogWarning("[Player_Heal] 'Heal' 액션이 없습니다.");
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
            // 강제 종료 태그
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

    /// <summary>힐 강제 취소. refundCharge=true 시 차지 1회 반환.</summary>
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

        // 태그: 종료
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
        OnTag?.Invoke(TAG_HEAL_START); // 태그: 시작

        combat?.BlockStaminaRegenFor(healDuration);
        combat?.StartActionLock(healDuration, true);

        moveRef?.AddMovementLock(LOCK_HEAL, false, true);
        if (animator) animator.SetTrigger("HealStart");

        yield return new WaitForSeconds(healDuration);

        // === 실제 힐 적용 ===
        if (combat != null) combat.Heal(+combat.HPMax * healAmount);

        // 🔸 힐 성공 VFX (딱 여기만 추가)
        SpawnHealSuccessVFX();

        IsHealing = false;
        moveRef?.RemoveMovementLock(LOCK_HEAL, false);
        healCo = null;

        OnTag?.Invoke(TAG_HEAL_END); // 태그: 종료
    }

    private void SpawnHealSuccessVFX()
    {
        if (!healSuccessVFX) return;

        var go = Instantiate(healSuccessVFX, transform.position, Quaternion.identity);

        // VFX가 좌/우 반전을 따라가도록(있으면 사용)
        var sr = GetComponentInChildren<SpriteRenderer>();
        var follower = go.GetComponent<VFXFollowFlip>();
        if (sr && follower == null) follower = go.AddComponent<VFXFollowFlip>();

        if (follower != null)
        {
            follower.Init(transform, sr, healVfxOffset, healVfxAttachToPlayer);
        }
        else
        {
            if (healVfxAttachToPlayer) { go.transform.SetParent(transform, true); go.transform.localPosition += (Vector3)healVfxOffset; }
            else { go.transform.position += (Vector3)healVfxOffset; }
        }

        if (healVfxLifetime > 0f) Destroy(go, healVfxLifetime);
    }
}
