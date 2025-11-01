using UnityEngine;
using System.Collections;

public class EnemyCore_01 : MonoBehaviour, IParryable, IDamageable
{
    // ──────────────────────────────────────────────────────────────
    #region Common & References
    [Header("Common")]
    [SerializeField] private float AttackCooldown = 2f;
    public Transform Player;

    [Header("Stats")]
    public float MaxHp = 100f;
    public float CurrentHp = 100f;
    public float MaxStamina = 100f;
    public float CurrentStamina = 0f;

    [Header("Guard Settings")]
    [SerializeField] private float GuardResetDelay = 2f;
    private bool IsGuarding = false;
    private float lastGuardTime = -999f;
    private Coroutine guardResetCo;
    public int guardCount = 0;
    private float lastHitTime = -999f;
    private int guardBlockCount = 0;

    [Header("AI Movement")]
    [SerializeField] public float RecognizedArea = 10f;
    [SerializeField] public float Speed = 3.0f;
    [SerializeField] public float MinChaseDistance = 1.3f;
    [SerializeField] public Collider2D MovementArea;
    private bool _isGroggy = false;
    [SerializeField] private float groggyDuration = 3f;

    [Header("Runtime")]
    public float AttackTimer = 0f;
    private bool _isHit = false;
    private bool _isDead = false;
    private float _noMoveRemain = 0f;

    public Rigidbody2D Rb { get; private set; }
    private Animator anim;
    private EnemyFight_01 _combat;

    [Header("Hit Window")]
    [SerializeField] private float hitActiveDuration = 0.08f;
    private bool _hitWindowOpen = false;
    private bool _hitAppliedThisWindow = false;
    private bool _currentParryable = true;
    public bool AllowParry = true;

    private bool isWeaveAttacking = false;

    [Header("Game References")]
    [SerializeField] private GameManager _gm;
    public bool IsDead() => _isDead;

    public enum MeleeAttackType { One, OneOne, OneTwo, Roll }
    public enum RangeAttackType { Short, Mid, Long }
    public enum AttackSelectMode
    {
        Random,
        Melee_One,
        Melee_OneOne,
        Melee_OneTwo,
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
    // ──────────────────────────────────────────────────────────────


    #region Unity Lifecycle
    private void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
        _combat = GetComponent<EnemyFight_01>();
        if (_combat == null) _combat = gameObject.AddComponent<EnemyFight_01>();
        _combat.BindCore(this);
        anim = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        AttackTimer = 0f;
        Rb.position = ClampInside(Rb.position);
    }

    private void Update()
    {
        if (_isDead) return;
        if (_noMoveRemain > 0f) _noMoveRemain -= Time.deltaTime;

        if (!_isGroggy && CurrentStamina >= 100f)
        {
            StartCoroutine(EnterGroggy());
        }

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

        if (Input.GetKey(KeyCode.Space))
        {
            Die();
        }
    }

    private void FixedUpdate()
    {
        if (_isGroggy)
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        if (_isDead)
        {
            return;
        }

        if (_noMoveRemain > 0f || IsAnimationPlaying())
        {
            Rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!IsActing)
            AIMoveMent();
    }
    #endregion
    // ──────────────────────────────────────────────────────────────


    #region Hit & Guard System
    public void ApplyHit(float damage, float knockback, Vector2 direction, GameObject source)
    {
        if (_isDead) return;

        Vector3 hitSource = source != null ? source.transform.position : transform.position;
        AnimatorStateInfo info = anim != null ? anim.GetCurrentAnimatorStateInfo(0) : default;

        if (info.IsName("Weave") || info.IsName("WeaveAttack"))
            return;

        if (IsGuarding || info.IsName("Guard"))
        {
            lastGuardTime = Time.time;
            OnGuarded(hitSource);
            anim.Play("Enemy_01_Guard", 0, 0f);
            guardCount += 1;
            CurrentStamina += 10;
            if (source != null && guardCount >= 6 && Random.value <= 0.6f)
            {
                OnParried(hitSource);
                guardCount = 0;
            }
            return;
        }

        bool recentlyHit = Time.time - lastHitTime < 1f;
        if (!IsGuarding && !recentlyHit && Random.value <= 0.5f)
        {
            StartGuard(hitSource);
            OnGuarded(hitSource);

            return;
        }
        else if (recentlyHit)
        {
            guardBlockCount++;

            if (guardBlockCount >= 5)
            {
                guardBlockCount = 0;
                StartGuard(hitSource);
                OnGuarded(hitSource);
                guardCount++;
                return;
            }
}

        CurrentHp -= damage;
        StartCoroutine(HitStop(0.1f));

        _isHit = true;
        StartCoroutine(ResetHitFlag(0.3f));

        PlayRandomHit();

        lastHitTime = Time.time;
    }

    private IEnumerator HitStop(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    private void StartGuard(Vector3 attackSource)
    {
        IsGuarding = true;
        lastGuardTime = Time.time;
        anim?.SetBool("IsGuarding", true);
        CurrentStamina += 20;

        if (guardResetCo != null)
            StopCoroutine(guardResetCo);

        guardResetCo = StartCoroutine(GuardResetTimer());
    }

    private IEnumerator GuardResetTimer()
    {
        while (true)
        {
            if (IsGuarding && Time.time - lastGuardTime >= GuardResetDelay)
            {
                StopGuard();
                yield break;
            }

            if (!IsGuarding)
                yield break;

            yield return null;
        }
    }

    private void StopGuard()
    {
        if (!IsGuarding) return;
        IsGuarding = false;
        anim?.SetBool("IsGuarding", false);

        if (guardResetCo != null)
        {
            StopCoroutine(guardResetCo);
            guardResetCo = null;
        }
    }

    public void OnGuarded(Vector3 guardSourcePosition)
    {
        lastGuardTime = Time.time;

        if (Rb != null)
        {
            Vector2 knockbackDir = ((Vector2)transform.position - (Vector2)Player.position).normalized;
            Rb.AddForce(knockbackDir * 1.5f, ForceMode2D.Impulse);
        }

        _combat?.InterruptDash();
        StartNoMoveCooldown(0.4f);
        IsActing = false;
        AttackTimer = 0f;
    }

    private IEnumerator ResetHitFlag(float delay)
    {
        yield return new WaitForSeconds(delay);
        _isHit = false;
    }

    private void PlayRandomHit()
    {
        if (anim == null) return;
        int rand = Random.Range(1, 4);
        anim.Play($"Enemy_01_Hit0{rand}", 0, 0);
    }
    #endregion
    // ──────────────────────────────────────────────────────────────


    #region Parry System
    public void OnParried(Vector3 parrySourcePosition)
    {
        if (isWeaveAttacking) return;

        anim?.SetTrigger("Weave");

        if (Player != null && Player.TryGetComponent<PlayerCombat>(out var pc))
        {
            pc.AddStamina(10f);
            pc.AddStamina(-5f);
        }

        _combat?.InterruptDash();
        StartCoroutine(DelayedWeaveAttackTrigger());

        StartNoMoveCooldown(0.6f);
        IsActing = false;
        AttackTimer = 0f;
        guardCount = 0;
    }

    private IEnumerator DelayedWeaveAttackTrigger()
    {
        if (isWeaveAttacking) yield break;
        isWeaveAttacking = true;

        yield return new WaitForSeconds(0.3f);
        anim?.SetTrigger("WeaveAttack");
        yield return OpenHitWindow();

        isWeaveAttacking = false;
    }
    #endregion
    // ──────────────────────────────────────────────────────────────


    #region Damage Window
    private Coroutine damageCoroutine;
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
    #endregion
    // ──────────────────────────────────────────────────────────────


    #region Movement & Death
    private bool IsAnimationPlaying()
    {
        if (anim == null) return false;
        AnimatorStateInfo state = anim.GetCurrentAnimatorStateInfo(0);
        return state.IsName("Attack") || state.IsName("Hit") || state.IsName("Guard") ||
               state.IsName("Weave") || state.IsName("WeaveAttack") || state.IsName("OneReady")
               || state.IsName("OneTwoReady") || state.IsName("RollReady");
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

        anim.Play("Enemy_01_Death", 0, 0f);

        Vector2 knockbackDir = ((Vector2)transform.position - (Vector2)Player.position).normalized;
        Rb.AddForce(knockbackDir * 12f, ForceMode2D.Impulse);

        StartCoroutine(TransitionToDie(0.6f));
    }

    private IEnumerator TransitionToDie(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);

        if (anim != null)
        {
            anim.SetTrigger("Die");
        }

        Rb.linearVelocity = Vector2.zero;
        Rb.simulated = false;
        Destroy(gameObject, 2f);
    }

    private IEnumerator DeathMotion(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        anim?.SetTrigger("Die");
        Rb.linearVelocity = Vector2.zero;
        Rb.simulated = false;
        Destroy(gameObject, 1f);
    }
    #endregion
    // ──────────────────────────────────────────────────────────────


    #region Utility & Attack Selection
    public Vector2 ClampInside(Vector2 p)
    {
        if (MovementArea == null) return p;
        Vector2 closest = MovementArea.ClosestPoint(p);
        Vector2 center = MovementArea.bounds.center;
        Vector2 inward = (center - closest).sqrMagnitude > 1e-8f ? (center - closest).normalized : Vector2.zero;
        return closest + inward * 0.14f;
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
            case AttackSelectMode.Melee_One: _combat.Melee_Attack(MeleeAttackType.One); CurrentStamina -= 4; break;
            case AttackSelectMode.Melee_OneOne: _combat.Melee_Attack(MeleeAttackType.OneOne); CurrentStamina -= 6;break;
            case AttackSelectMode.Melee_OneTwo: _combat.Melee_Attack(MeleeAttackType.OneTwo); CurrentStamina -= 8; break;
            case AttackSelectMode.Melee_Roll: _combat.Melee_Attack(MeleeAttackType.Roll); CurrentStamina -= 10;break;
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
    #endregion
}
