using UnityEngine;
using System.Collections;

public class EnemyCore_02 : MonoBehaviour, IParryable, IDamageable
{
    #region Common & References
    [Header("Common")]
    [SerializeField] private float AttackCooldown = 2f;
    public Transform Player;

    [Header("Stats")]
    public float MaxHp = 100f;
    public float CurrentHp = 100f;
    public float MaxStamina = 100f;
    public float CurrentStamina = 0f;

    [Header("AI Movement")]
    [SerializeField] public float RecognizedArea = 10f;
    [SerializeField] public float Speed = 3.0f;
    [SerializeField] public float MinChaseDistance = 1.3f;
    [SerializeField] public Collider2D MovementArea;
    [SerializeField] private float groggyDuration = 3f;
    private bool _isGroggy = false;

    [Header("Runtime")]
    public float AttackTimer = 0f;
    private bool _isHit = false;
    private bool _isDead = false;
    private float _noMoveRemain = 0f;

    public Rigidbody2D Rb { get; private set; }
    private Animator anim;
    private EnemyFight_02 _combat;

    [Header("Hit Window")]
    [SerializeField] private float hitActiveDuration = 0.08f;
    private bool _hitWindowOpen = false;
    private bool _hitAppliedThisWindow = false;
    private bool _currentParryable = true;
    public bool AllowParry = true;

    [Header("Game References")]
    [SerializeField] private GameManager _gm;
    public bool IsDead() => _isDead;

    public enum MeleeAttackType { Slam1, Slam2, Slam3, Roll }
    public enum RangeAttackType { Short, Mid, Long }
    public enum AttackSelectMode
    {
        Random,
        Melee_Slam1,
        Melee_Slam2,
        Melee_Slam3,
        Melee_Roll,
        Range_Short,
        Range_Mid,
        Range_Long
    }

    [Header("Attack Select (Debug)")]
    public AttackSelectMode SelectMode = AttackSelectMode.Random;
    public MeleeAttackType LastMeleeType { get; set; }
    public bool IsActing { get; set; }
    #endregion

    private void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        _combat = GetComponent<EnemyFight_02>();
        if (_combat == null) _combat = gameObject.AddComponent<EnemyFight_02>();
        _combat.BindCore(this);
    }

    private void Start()
    {
        AttackTimer = 0f;
    }

    private void Update()
    {
        if (_isDead) return;

        if (_noMoveRemain > 0f)
            _noMoveRemain -= Time.deltaTime;

        if (!_isGroggy && CurrentStamina >= 100f)
            StartCoroutine(EnterGroggy());

        Rb.position = ClampInside(Rb.position);

        if (!IsActing && !_isHit)
        {
            AttackTimer += Time.deltaTime;

            if (Player != null)
            {
                float distanceToPlayer = Vector2.Distance(Rb.position, Player.position);
                if (distanceToPlayer <= RecognizedArea && AttackTimer >= AttackCooldown)
                {
                    AttackWayChoice();
                    AttackTimer = 0f;
                }
            }
        }

        if (CurrentHp <= 0)
            Die();
    }

    private void FixedUpdate()
    {
        if (_isDead) return;

        if (_isGroggy)
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        if (_noMoveRemain > 0f || IsAnimationBlocking())
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!IsActing)
            AIMoveMent();
    }

    public void ApplyHit(float damage, float knockback, Vector2 direction, GameObject source)
    {
        if (_isDead) return;

        CurrentHp -= damage;
        StartCoroutine(HitStop(0.1f));

        _isHit = true;
        StartCoroutine(ResetHitFlag(0.3f));
    }

    private IEnumerator HitStop(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    private IEnumerator ResetHitFlag(float delay)
    {
        yield return new WaitForSeconds(delay);
        _isHit = false;
    }

    public void SetPhysicsDuringAttack(bool isAttacking)
    {
        if (Rb == null) return;
        Rb.bodyType = isAttacking ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        Rb.linearVelocity = Vector2.zero;
    }

    public void OnParried(Vector3 parrySourcePosition)
    {
        _combat?.InterruptDash();
        Rb.linearVelocity = Vector2.zero;
        StartNoMoveCooldown(0.3f);
        Vector2 knockbackDir = ((Vector2)transform.position - (Vector2)parrySourcePosition).normalized;
        Rb.AddForce(knockbackDir * 3f, ForceMode2D.Impulse);
        IsActing = true;
        AttackTimer = 0f;
    }

    public void TriggerMeleeDamage()
    {
        if (_hitWindowOpen) return;
        StartCoroutine(DoMeleeDamage());
    }

    private IEnumerator DoMeleeDamage()
    {
        yield return OpenHitWindow();
    }

    private IEnumerator OpenHitWindow()
    {
        if (_hitWindowOpen) yield break;
        _currentParryable = AllowParry;
        _hitAppliedThisWindow = false;
        _hitWindowOpen = true;

        TryApplyHit_OnlyIfInRange();

        yield return new WaitForSeconds(hitActiveDuration);

        _hitWindowOpen = false;
        _hitAppliedThisWindow = false;
    }

    private void TryApplyHit_OnlyIfInRange()
    {
        if (Player == null) return;

        Vector2 center = transform.position;
        float radius = 1.2f;
        LayerMask playerMask = LayerMask.GetMask("Player");

        foreach (var hit in Physics2D.OverlapCircleAll(center, radius, playerMask))
        {
            var ph = hit.GetComponent<PlayerHit>();
            if (ph != null)
            {
                Vector2 hitDir = (hit.transform.position - transform.position).normalized;
                ph.OnHit(10f, 6f, hitDir, _currentParryable, gameObject);
                _hitAppliedThisWindow = true;
            }
        }
    }

    private bool IsAnimationBlocking()
    {
        if (anim == null) return false;
        var st = anim.GetCurrentAnimatorStateInfo(0);
        return st.IsName("SlamReady") || st.IsName("Slam2Ready") || st.IsName("Slam3Ready");
    }

    public void AIMoveMent()
    {
        if (Player == null) { Rb.linearVelocity = Vector2.zero; return; }

        Vector2 toPlayer = (Vector2)Player.position - Rb.position;
        float dist = toPlayer.magnitude;

        if (dist < MinChaseDistance)
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        if (dist <= RecognizedArea)
            Rb.linearVelocity = toPlayer.normalized * Speed;
        else
            Rb.linearVelocity = Vector2.zero;
    }

    private IEnumerator EnterGroggy()
    {
        _isGroggy = true;
        IsActing = true;
        Rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(groggyDuration);
        ExitGroggy();
    }

    private void ExitGroggy()
    {
        _isGroggy = false;
        IsActing = false;
        CurrentStamina = 0f;
    }

    private void Die()
    {
        if (_isDead) return;
        _isDead = true;
        IsActing = true;
        anim.Play("Enemy_02_Die", 0, 0f);

        StartCoroutine(TransitionToDie(0.6f));
    }

    private IEnumerator TransitionToDie(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        Rb.linearVelocity = Vector2.zero;
        Rb.simulated = false;
        Destroy(gameObject, 2f);
    }

    public Vector2 ClampInside(Vector2 p)
    {
        if (MovementArea == null)
            return p;
        if (MovementArea.OverlapPoint(p))
            return p;

        Vector2 closest = MovementArea.ClosestPoint(p);
        Vector2 center = MovementArea.bounds.center;
        Vector2 inward = (center - closest).sqrMagnitude > 1e-8f ? (center - closest).normalized : Vector2.zero;
        return closest + inward * 0.1f;
    }

    public void StartNoMoveCooldown(float seconds)
    {
        _noMoveRemain = Mathf.Max(_noMoveRemain, Mathf.Max(0f, seconds));
    }

    private void AttackWayChoice()
    {
        if (_combat == null) return;

        switch (SelectMode)
        {
            case AttackSelectMode.Random:
                if (Random.Range(0, 2) == 0)
                    _combat.Melee_Attack_DistanceBased();
                else
                    _combat.Range_Attack();
                break;
            case AttackSelectMode.Melee_Slam1: _combat.Melee_Attack(MeleeAttackType.Slam1); if (CurrentStamina > 0) CurrentStamina -= 4; break;
            case AttackSelectMode.Melee_Slam2: _combat.Melee_Attack(MeleeAttackType.Slam2); if (CurrentStamina > 0) CurrentStamina -= 8; break;
            case AttackSelectMode.Melee_Slam3: _combat.Melee_Attack(MeleeAttackType.Slam3); if (CurrentStamina > 0) CurrentStamina -= 10; break;
            case AttackSelectMode.Melee_Roll: _combat.Melee_Attack(MeleeAttackType.Roll); if (CurrentStamina > 0) CurrentStamina -= 14; break;
            case AttackSelectMode.Range_Short: _combat.Range_Attack(RangeAttackType.Short); break;
            case AttackSelectMode.Range_Mid: _combat.Range_Attack(RangeAttackType.Mid); break;
            case AttackSelectMode.Range_Long: _combat.Range_Attack(RangeAttackType.Long); break;
        }
    }

    public void ForceNextAction()
    {
        IsActing = false;
        AttackTimer = AttackCooldown;
    }
}