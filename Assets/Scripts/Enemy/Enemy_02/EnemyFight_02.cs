using UnityEngine;
using System.Collections;

public class EnemyFight_02 : MonoBehaviour
{
    [Header("Dash (Melee)")]
    [SerializeField] private float DashSpeed = 12f;
    [SerializeField] private float PreWindupShort = 2f;
    [SerializeField] private float PreWindupMid = 3f;
    [SerializeField] private float PreWindupLong = 1f;
    [SerializeField] private float PreWindupRoll = 1f;

    [Header("Melee Logic")]
    [SerializeField] private float NoDashCloseRange = 2.5f;
    [SerializeField] private float StopOffset = 1.0f;

    [Header("Melee Stop Settings")]
    [SerializeField] private float DashStopDistance = 1.15f;
    [SerializeField] private float MaxDashTime = 0.45f;
    [SerializeField] private bool LockDashDirection = false;

    [Header("Dash (Range)")]
    [SerializeField] private float RetreatSpeed = 6f;
    [SerializeField] private float RetreatDuration = 1.25f;
    [SerializeField] private float RangePreWindupShort = 1.0f;
    [SerializeField] private float RangePreWindupMid = 1.3f;
    [SerializeField] private float RangePreWindupLong = 1.5f;

    [Header("Projectile (Ranged Attack)")]
    [SerializeField] private GameObject ProjectilePrefab;
    [SerializeField] private Transform FirePoint;
    [SerializeField] private int VolleyCountShort = 1;
    [SerializeField] private int VolleyCountMid = 2;
    [SerializeField] private int VolleyCountLong = 3;
    [SerializeField] private float VolleyInterval = 0.2f;

    [Header("Post-Snap Control")]
    [SerializeField] private float SnapNoMoveDuration = 0.8f;

    private EnemyCore_02 _core;
    private SpriteRenderer Sprite;
    private Animator anim;

    private float _curPreWindup = 2f;
    private float _desiredDistance = 7f;
    private int _volleyCount = 1;
    private float _rangePreWindup = 1.0f;

    public bool isDashing = false;
    public Transform player;

    //───────────────────────────────────────────────────────────────
    void Start()
    {
        Sprite = GetComponent<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();
        if (_core == null)
            _core = GetComponent<EnemyCore_02>();
    }

    void Update()
    {
        if (player == null || _core == null) return;
        if (_core.IsDead()) return;

        Sprite.flipX = player.position.x < transform.position.x;
    }

    public void BindCore(EnemyCore_02 core) => _core = core;

    public void InterruptDash()
    {
        isDashing = false;
        if (_core == null) return;
        _core.Rb.linearVelocity = Vector2.zero;
        _core.IsActing = false;
        _core.ForceNextAction();
    }

    //───────────────────────────────────────────────────────────────
    #region Melee Attack
    public void Melee_Attack()
    {
        if (_core == null || _core.IsActing) return;
        Melee_Attack_DistanceBased();
    }

    public void Melee_Attack_DistanceBased()
    {
        if (_core == null || _core.IsActing) return;

        float dist = (_core.Player != null)
            ? Vector2.Distance(_core.Rb.position, _core.Player.position)
            : Mathf.Infinity;

        if (dist > NoDashCloseRange)
        {
            int pick = Random.Range(0, 2);
            Melee_Attack(pick == 0 ? EnemyCore_02.MeleeAttackType.Slam1
                                   : EnemyCore_02.MeleeAttackType.Slam2);
        }
        else
        {
            Melee_Attack(EnemyCore_02.MeleeAttackType.Slam3);
        }
    }

    public void Melee_Attack(EnemyCore_02.MeleeAttackType type)
    {
        if (_core == null || _core.IsActing) return;
        _core.IsActing = true;
        _core.SetPhysicsDuringAttack(true);
        _core.LastMeleeType = type;

        switch (type)
        {
            case EnemyCore_02.MeleeAttackType.Slam1:
                _curPreWindup = PreWindupShort;
                StartCoroutine(MeleeDash());
                break;
            case EnemyCore_02.MeleeAttackType.Slam2:
                _curPreWindup = PreWindupMid;
                StartCoroutine(MeleeDash());
                break;
            case EnemyCore_02.MeleeAttackType.Slam3:
                _curPreWindup = PreWindupLong;
                StartCoroutine(MeleeStrikeNoDash());
                break;
            case EnemyCore_02.MeleeAttackType.Roll:
                _curPreWindup = PreWindupRoll;
                StartCoroutine(MeleeStrikeRoll());
                break;
        }
    }

    private IEnumerator MeleeDash()
    {
        isDashing = true;
        _core.Rb.linearVelocity = Vector2.zero;
        EnemyCore_02.MeleeAttackType type = _core.LastMeleeType;
        if (type == EnemyCore_02.MeleeAttackType.Slam1)
        {
            anim.SetTrigger("SlamReady");
        }
        else if (type == EnemyCore_02.MeleeAttackType.Slam2)
        {
            anim.SetTrigger("Slam2Ready");
        }

        if (_curPreWindup > 0f) yield return new WaitForSeconds(_curPreWindup);

        Vector2 lockedDir = (_core.Player != null)
            ? ((Vector2)_core.Player.position - _core.Rb.position).normalized
            : Vector2.right;

        float elapsed = 0f;
        while (isDashing)
        {
            if (_core.Player == null) break;
            Vector2 toPlayer = (Vector2)_core.Player.position - _core.Rb.position;
            float dist = toPlayer.magnitude;
            if (dist <= DashStopDistance) break;

            Vector2 dir = LockDashDirection ? lockedDir : toPlayer.normalized;
            _core.Rb.linearVelocity = dir * DashSpeed;

            elapsed += Time.fixedDeltaTime;
            if (elapsed >= MaxDashTime) break;
            yield return new WaitForFixedUpdate();
        }

        _core.Rb.linearVelocity = Vector2.zero;
        if (Vector2.Distance(_core.Rb.position, _core.Player.position) <= DashStopDistance + 0.2f)
            StopInFrontOfPlayer();

        yield return StartCoroutine(DashFinishStrikeSequence());

        isDashing = false;
        _core.IsActing = false;
        _core._isHit = false;
        _core.SetPhysicsDuringAttack(false);
        ResetAnim();
    }

    private IEnumerator MeleeStrikeNoDash()
    {
        _core.Rb.linearVelocity = Vector2.zero;
        anim.SetTrigger("Slam3Ready");

        if (_curPreWindup > 0f)
            yield return new WaitForSeconds(_curPreWindup);

        yield return StartCoroutine(DashFinishStrikeSequence());
        _core.IsActing = false;
        _core._isHit = false;
        _core.SetPhysicsDuringAttack(false);
        ResetAnim();
    }

    private IEnumerator MeleeStrikeRoll()
    {
        _core.Rb.linearVelocity = Vector2.zero;
        anim?.SetTrigger("RollReady");

        if (_curPreWindup > 0f)
            yield return new WaitForSeconds(_curPreWindup);

        anim?.SetBool("Roll", true);
        for (int i = 0; i < 5; i++)
        {
            _core.TriggerMeleeDamage();
            if (_core.Player != null)
            {
                Vector2 dir = ((Vector2)_core.Player.position - _core.Rb.position).normalized;
                Vector2 nextPos = _core.Rb.position + dir * 0.5f;
                _core.Rb.position = _core.ClampInside(nextPos);
            }
            yield return new WaitForSeconds(0.3f);
        }

        _core.IsActing = false;
        anim?.SetBool("Roll", false);
        _core._isHit = false;
        _core.SetPhysicsDuringAttack(false);
        ResetAnim();
    }
    #endregion

    //───────────────────────────────────────────────────────────────
    #region Ranged Attack
    public void Range_Attack()
    {
        if (_core == null || _core.IsActing) return;
        var type = (EnemyCore_02.RangeAttackType)Random.Range(0, 3);
        Range_Attack(type);
    }

    public void Range_Attack(EnemyCore_02.RangeAttackType type)
    {
        if (_core == null || _core.IsActing) return;

        switch (type)
        {
            case EnemyCore_02.RangeAttackType.Short:
                _desiredDistance = 5f; _rangePreWindup = RangePreWindupShort; _volleyCount = VolleyCountShort; break;
            case EnemyCore_02.RangeAttackType.Mid:
                _desiredDistance = 7f; _rangePreWindup = RangePreWindupMid; _volleyCount = VolleyCountMid; break;
            case EnemyCore_02.RangeAttackType.Long:
                _desiredDistance = 9f; _rangePreWindup = RangePreWindupLong; _volleyCount = VolleyCountLong; break;
        }

        _core.IsActing = true;
        StartCoroutine(RetreatThenFire());
    }

    private IEnumerator RetreatThenFire()
    {
        float t = 0f;
        while (t < RetreatDuration)
        {
            if (_core.Player == null) break;

            Vector2 toPlayer = (Vector2)_core.Player.position - _core.Rb.position;
            Vector2 dirAway = (-toPlayer).normalized;
            Vector2 nextPos = _core.ClampInside(_core.Rb.position + dirAway * RetreatSpeed * Time.fixedDeltaTime);

            _core.Rb.MovePosition(nextPos);
            if (_desiredDistance > 0f && toPlayer.magnitude >= _desiredDistance) break;

            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        _core.Rb.linearVelocity = Vector2.zero;
        if (_rangePreWindup > 0f) yield return new WaitForSeconds(_rangePreWindup);

        for (int i = 0; i < _volleyCount; i++)
        {
            FireOneProjectile();
            if (i < _volleyCount - 1 && VolleyInterval > 0f)
                yield return new WaitForSeconds(VolleyInterval);
        }

        _core.IsActing = false;
        _core.ForceNextAction();
    }
    #endregion

    //───────────────────────────────────────────────────────────────
    #region Utility
    private IEnumerator DashFinishStrikeSequence()
    {
        switch (_core.LastMeleeType)
        {
            case EnemyCore_02.MeleeAttackType.Slam1:
                yield return new WaitForSeconds(0.1f);
                anim.SetBool("Slam", true);
                _core.TriggerMeleeDamage();
                yield return new WaitForSeconds(0.3f);
                anim.SetBool("Slam2", true);
                _core.TriggerMeleeDamage();
                yield return new WaitForSeconds(1.2f);
                anim.SetBool("Slam3", true);
                _core.TriggerMeleeDamage();
                yield return new WaitForSeconds(0.5f);
                break;

            case EnemyCore_02.MeleeAttackType.Slam2:
                yield return new WaitForSeconds(0.1f);
                anim.SetBool("Slam2", true);
                _core.TriggerMeleeDamage();
                yield return new WaitForSeconds(1f);
                anim.SetBool("Slam3", true);
                _core.TriggerMeleeDamage();
                yield return new WaitForSeconds(0.5f);
                break;

            case EnemyCore_02.MeleeAttackType.Slam3:
                yield return new WaitForSeconds(0.1f);
                anim.SetBool("Slam3", true);
                _core.TriggerMeleeDamage();
                yield return new WaitForSeconds(0.5f);
                break;

            default:
                _core.TriggerMeleeDamage();
                break;
        }
    }

    private void StopInFrontOfPlayer()
    {
        if (_core.Player == null || _core.Rb == null) return;

        Vector2 dir = ((Vector2)_core.Player.position - _core.Rb.position);
        dir = (dir.sqrMagnitude > 1e-8f) ? dir.normalized : Vector2.right;

        Vector2 stopPos = (Vector2)_core.Player.position - dir * StopOffset;
        _core.Rb.position = _core.ClampInside(stopPos);
        _core.Rb.linearVelocity = Vector2.zero;
        _core.IsActing = false;

        _core.StartNoMoveCooldown(SnapNoMoveDuration);
    }

    private void FireOneProjectile()
    {
        if (ProjectilePrefab == null || FirePoint == null) return;

        GameObject proj = Instantiate(ProjectilePrefab, FirePoint.position, Quaternion.identity);

        float life = 1f;

        switch (_core.SelectMode)
        {
            case EnemyCore_02.AttackSelectMode.Range_Short:
                life = 1f;
                break;
            case EnemyCore_02.AttackSelectMode.Range_Mid:
                life = 2f;
                break;
            case EnemyCore_02.AttackSelectMode.Range_Long:
                life = 3f;
                break;
        }

        Destroy(proj, life);
    }

    private void ResetAnim()
    {
        anim.ResetTrigger("SlamReady");
        anim.ResetTrigger("Slam2Ready");
        anim.ResetTrigger("Slam3Ready");
        anim.SetBool("Slam", false);
        anim.SetBool("Slam2", false);
        anim.SetBool("Slam3", false);
        anim.SetBool("Roll", false);
    }
    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_core == null) _core = GetComponent<EnemyCore_02>();
        if (_core == null) return;

        Vector3 pos = _core.Rb != null ? (Vector3)_core.Rb.position : transform.position;

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
        Gizmos.DrawWireSphere(pos, NoDashCloseRange);

        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.5f);
        Gizmos.DrawWireSphere(pos, DashStopDistance);
    }
#endif
}
