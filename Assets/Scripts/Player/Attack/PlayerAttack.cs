using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 공격 허브: 입력 라우팅, 서브모듈 바인딩, 콤보/카운터 전달.
/// 최적화:
/// - Awake에서 1회 바인딩(중복 제거)
/// - 입력 액션 안전 구독/해제
/// - 레퍼런스 지연 초기화/보장
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    [Header("Action Names")]
    [SerializeField] private string attackActionName = "Attack";
    [SerializeField] private string chargeActionName = "Charge";

    [Header("Sub Modules")]
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

    [Header("Runtime Tunables")]
    [SerializeField] public AttackBaseStats baseStats = new AttackBaseStats();

    [Header("Counter / Weaving")]
    [SerializeField] private int weavingIndexForCounter = 1; // 위빙 성공 시 카운터 애니 인덱스 매칭
    public int GetWeavingIndexForCounter() => Mathf.Max(1, weavingIndexForCounter);

    public bool IsAttacking { get; private set; }

    // Input
    private PlayerMove inputWrapper;
    private InputAction attackAction;
    private InputAction chargeAction;

    // init guard
    private bool _bound;

    // ---------- Lifecycle ----------
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
        // 지연 초기화
        EnsureRefs();
        // 한 번만 바인딩
        Bind(combat, moveRef, animator);

        // 입력 래퍼 준비(한 번만 생성)
        inputWrapper = new PlayerMove();
    }

    private void OnEnable()
    {
        if (inputWrapper == null) inputWrapper = new PlayerMove();
        inputWrapper.Enable();

        // 액션 캐시 & 구독
        var map = inputWrapper.asset;
        if (map != null)
        {
            attackAction = map.FindAction(attackActionName);
            if (attackAction != null)
            {
                attackAction.started += OnAttackStarted;
                attackAction.canceled += OnAttackCanceled;
            }
            else
            {
                Debug.LogWarning($"[PlayerAttack] '{attackActionName}' action not found.");
            }

            chargeAction = map.FindAction(chargeActionName);
            if (chargeAction != null)
            {
                chargeAction.started += OnChargeStarted;
                chargeAction.canceled += OnChargeCanceled;
            }
            else
            {
                Debug.LogWarning($"[PlayerAttack] '{chargeActionName}' action not found.");
            }
        }
        else
        {
            Debug.LogWarning("[PlayerAttack] Input actions asset missing.");
        }
    }

    private void OnDisable()
    {
        // 안전 해제
        if (attackAction != null)
        {
            attackAction.started -= OnAttackStarted;
            attackAction.canceled -= OnAttackCanceled;
            attackAction = null;
        }
        if (chargeAction != null)
        {
            chargeAction.started -= OnChargeStarted;
            chargeAction.canceled -= OnChargeCanceled;
            chargeAction = null;
        }
        inputWrapper?.Disable();
    }

    // ---------- Init / Bind ----------
    private void EnsureRefs()
    {
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();

        if (!normalAtk) normalAtk = GetComponent<N_ATK>();
        if (!chargeAtk) chargeAtk = GetComponent<CG_ATK>();
        if (!counterAtk) counterAtk = GetComponent<C_ATK>();
    }

    /// <summary>외부에서 레퍼런스 주입 시 사용(한 번만 수행)</summary>
    public void Bind(PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        if (_bound && combat == c && moveRef == m && animator == a) return;

        combat = c ?? combat ?? GetComponent<PlayerCombat>();
        moveRef = m ?? moveRef ?? GetComponent<PlayerMoveBehaviour>();
        animator = a ?? animator ?? GetComponent<Animator>();

        // 서브모듈에도 동일 레퍼런스 1회 주입
        if (normalAtk) normalAtk.Bind(this, combat, moveRef, animator);
        if (chargeAtk) chargeAtk.Bind(this, combat, moveRef, animator);
        if (counterAtk) counterAtk.Bind(this, combat, moveRef, animator);

        _bound = true;
    }

    public void SetAttacking(bool value) => IsAttacking = value;

    // ---------- Input Handlers ----------
    private void OnAttackStarted(InputAction.CallbackContext _)
    {
        if (combat != null && combat.IsAttackLocked) return;

        // 카운터 창이 열려 있으면 우선 시도
        if (counterAtk != null && counterAtk.TryTriggerCounterOnAttackPress())
            return;

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

    // ---------- API (외부에서 사용) ----------
    public void FreezeComboTimerFor(float seconds)
    {
        normalAtk?.FreezeComboTimerFor(seconds);
    }

    /// <summary>Weaving(패링) 성공 시 같은 번호의 Counter를 재생하기 위함</summary>
    public void SetLastWeavingIndex(int idx)
    {
        weavingIndexForCounter = Mathf.Max(1, idx);
    }

    /// <summary>외부에서 카운터 창 열기</summary>
    public void ArmCounter(float windowSeconds)
    {
        counterAtk?.ArmCounter(windowSeconds);
    }
}
