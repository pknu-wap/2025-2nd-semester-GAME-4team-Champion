using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    [Header("Action Names")]
    [SerializeField] private string attackActionName = "Attack";
    [SerializeField] private string chargeActionName = "Charge";
    public bool IsAttacking { get; private set; }
    // Sub modules
    [SerializeField] private N_ATK normalAtk;
    [SerializeField] private CG_ATK chargeAtk;
    [SerializeField] private C_ATK counterAtk;

    [System.Serializable]
    public class AttackBaseStats
    {
        public float baseDamage = 10f;
        public float baseKnockback = 6f;
        public float baseRange = 0.9f;
        public float baseRadius = 0.6f;
    }

    [SerializeField] private int weavingIndexForCounter = 1;
    public int GetWeavingIndexForCounter() => Mathf.Max(1, weavingIndexForCounter);
    [Header("Runtime Tunables")]
    public AttackBaseStats baseStats = new AttackBaseStats();

    // input
    private PlayerMove inputWrapper;
    private InputAction attackAction;
    private InputAction chargeAction;

    public void Bind(PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        combat = c; moveRef = m; animator = a;
        normalAtk?.Bind(this, c, m, a);
        chargeAtk?.Bind(this, c, m, a);
        counterAtk?.Bind(this, c, m, a);
    }
    public void SetAttacking(bool value)
    {
        IsAttacking = value;
    }
    private void Reset()
    {
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();

        if (!normalAtk) normalAtk = GetComponent<N_ATK>();
        if (!chargeAtk) chargeAtk = GetComponent<CG_ATK>();
        if (!counterAtk) counterAtk = GetComponent<C_ATK>();
    }

    private void Awake()
    {
        if (!normalAtk) normalAtk = GetComponent<N_ATK>();
        if (!chargeAtk) chargeAtk = GetComponent<CG_ATK>();
        if (!counterAtk) counterAtk = GetComponent<C_ATK>();

        inputWrapper = new PlayerMove();
        Bind(combat, moveRef, animator);
    }

    private void OnEnable()
    {
        inputWrapper.Enable();

        attackAction = inputWrapper.asset.FindAction(attackActionName);
        if (attackAction != null)
        {
            attackAction.started += OnAttackStarted;
            attackAction.canceled += OnAttackCanceled;
        }
        else Debug.LogWarning("[PlayerAttack] Attack action not found.");

        chargeAction = inputWrapper.asset.FindAction(chargeActionName);
        if (chargeAction != null)
        {
            chargeAction.started += OnChargeStarted;
            chargeAction.canceled += OnChargeCanceled;
        }
        else Debug.LogWarning("[PlayerAttack] Charge action not found.");
    }

    private void OnDisable()
    {
        if (attackAction != null)
        {
            attackAction.started -= OnAttackStarted;
            attackAction.canceled -= OnAttackCanceled;
        }
        if (chargeAction != null)
        {
            chargeAction.started -= OnChargeStarted;
            chargeAction.canceled -= OnChargeCanceled;
        }
        inputWrapper.Disable();
    }

    private void OnAttackStarted(InputAction.CallbackContext _)
    {
        if (combat != null && combat.IsAttackLocked) return;

        // 카운터 창이 열려 있으면 우선 시도
        if (counterAtk && counterAtk.TryTriggerCounterOnAttackPress()) return;

        // 일반 콤보
        normalAtk?.OnAttackStarted();
    }

    private void OnAttackCanceled(InputAction.CallbackContext _)
    {
        if (combat != null && combat.IsAttackLocked) return;
        normalAtk?.OnAttackCanceled();
    }

    private void OnChargeStarted(InputAction.CallbackContext _)
    {
        chargeAtk?.OnChargeStarted();
    }

    private void OnChargeCanceled(InputAction.CallbackContext _)
    {
        chargeAtk?.OnChargeCanceled();
    }

    // Weaving(패링) 성공 시에 호출되어, 같은 번호의 Counter를 재생하기 위함
    public void SetLastWeavingIndex(int idx)
    {
        weavingIndexForCounter = Mathf.Max(1, idx);
    }

    // 외부에서 카운터 창 열기
    public void ArmCounter(float windowSeconds)
    {
        counterAtk?.ArmCounter(windowSeconds);
    }
}
