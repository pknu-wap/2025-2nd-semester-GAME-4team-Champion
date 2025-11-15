using UnityEngine;
using System.Collections;

public class EnemyFight_01 : MonoBehaviour
{
    [Header("Dash (Melee)")]
    [SerializeField] private float DashSpeed = 12f; 
    [SerializeField] private float PreWindupShort = 2f; 
    [SerializeField] private float PreWindupMid = 3f; 
    [SerializeField] private float PreWindupLong = 1f; 
    [SerializeField] private float PreWindupRoll = 1f; 

    [Header("Melee Logic")]
    [SerializeField] private float ChoiceDash = 2.5f;   
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

    private EnemyCore_01 _core;
    private SpriteRenderer Sprite;

    private float _curPreWindup = 2f;
    private float _desiredDistance = 7f;
    private int _volleyCount = 1;
    private float _rangePreWindup = 1.0f;

    public bool isDashing = false;
    private Animator anim;
    public Transform player;

    void Start()
    {
        Sprite = GetComponent<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (player == null) return;

        if (_core != null && _core.IsDead())
        {
            return;
        }

        if (player.position.x < transform.position.x)
        {
            Sprite.flipX = true;
        }
        else
        {
            Sprite.flipX = false;
        }
    }

    public void BindCore(EnemyCore_01 core) => _core = core;

    public void InterruptDash()
    {
        isDashing = false;
        if (_core != null)
        {
            _core.Rb.linearVelocity = Vector2.zero;
            _core.IsActing = false;
            _core.ForceNextAction();
        }
    }

    public void Melee_Attack_DistanceBased()
    {
        if (_core.IsActing) return;

        float dist = (_core.Player != null)
            ? Vector2.Distance(_core.Rb.position, (Vector2)_core.Player.position)
            : Mathf.Infinity;

        if (_core.CurrentHp < 30)
        {
            if (dist > ChoiceDash)
            {
                Melee_Attack(EnemyCore_01.MeleeAttackType.OneOne);
            }
            else
            {
                int pick = Random.Range(0, 2);
                Melee_Attack(pick == 0 ? EnemyCore_01.MeleeAttackType.Roll
                                    : EnemyCore_01.MeleeAttackType.OneTwo);
            }
        }
        else
        {
            if (dist > ChoiceDash)
            {
                int pick = Random.Range(0, 2);
                Melee_Attack(pick == 0 ? EnemyCore_01.MeleeAttackType.One
                                    : EnemyCore_01.MeleeAttackType.OneOne);
            }
            else
            {
                Melee_Attack(EnemyCore_01.MeleeAttackType.OneTwo);
            }
        }
            
    }

    private void ResetAnim()
    {
        anim.ResetTrigger("OneReady");
        anim.ResetTrigger("OneTwoReady");
        anim.ResetTrigger("RollReady");
        anim.SetBool("One", false);
        anim.SetBool("OneOne", false);
        anim.SetBool("OneTwo", false);
        anim.SetBool("Roll", false);
    }

    public void ForceInterruptAttack()
    {
        StopAllCoroutines();

        isDashing = false;
        if (_core != null)
        {
            _core.Rb.linearVelocity = Vector2.zero;
            _core.IsActing = false;
            _core.StartNoMoveCooldown(0.6f);
        }
        ResetAnim();
    }

    public void Melee_Attack()
    {
        if (_core.IsActing) return;
        Melee_Attack_DistanceBased();
    }

    public void Melee_Attack(EnemyCore_01.MeleeAttackType type)
    {
        if (_core.IsActing) return;
        _core.SetPhysicsDuringAttack(true);

        switch (type)
        {
            case EnemyCore_01.MeleeAttackType.One:
                _curPreWindup = PreWindupShort;
                _core.LastMeleeType = EnemyCore_01.MeleeAttackType.One;
                _core.IsActing = true;
                StartCoroutine(MeleeDash());
                break;
            case EnemyCore_01.MeleeAttackType.OneOne:
                _curPreWindup = PreWindupMid;
                _core.LastMeleeType = EnemyCore_01.MeleeAttackType.OneOne;
                _core.IsActing = true;
                StartCoroutine(MeleeDash());
                break;
            case EnemyCore_01.MeleeAttackType.OneTwo:
                _curPreWindup = PreWindupLong;
                _core.LastMeleeType = EnemyCore_01.MeleeAttackType.OneTwo;
                _core.IsActing = true;
                StartCoroutine(MeleeStrikeNoDash());
                break;
            case EnemyCore_01.MeleeAttackType.Roll:
                _curPreWindup = PreWindupRoll;
                _core.LastMeleeType = EnemyCore_01.MeleeAttackType.Roll;
                _core.IsActing = true;
                StartCoroutine(MeleeStrikeRoll());
                break;
        }
    }

    public bool Tutorial_Checker3;

    private IEnumerator MeleeDash()
    {
        isDashing = true;
        Tutorial_Checker3 = false;
        _core.Rb.linearVelocity = Vector2.zero;
        anim.SetTrigger("OneReady");

        if (_curPreWindup > 0f) yield return new WaitForSeconds(_curPreWindup);

        Vector2 lockedDir = Vector2.right;
        if (_core.Player != null)
        {
            Vector2 toP0 = (Vector2)_core.Player.position - _core.Rb.position;
            lockedDir = toP0.sqrMagnitude > 1e-8f ? toP0.normalized : Vector2.right;
        }

        float elapsed = 0f;
        while (isDashing) 
        {
            if (_core.Player == null) break;

            Vector2 toPlayer = (Vector2)_core.Player.position - _core.Rb.position;
            float dist = toPlayer.magnitude;

            if (dist <= DashStopDistance) break;

            Vector2 dir = LockDashDirection
                ? lockedDir
                : (toPlayer.sqrMagnitude > 1e-8f ? toPlayer.normalized : lockedDir);

            _core.Rb.linearVelocity = dir * DashSpeed;

            elapsed += Time.fixedDeltaTime;
            if (elapsed >= MaxDashTime) break;

            yield return new WaitForFixedUpdate();
        }

        _core.Rb.linearVelocity = Vector2.zero;

        float currentDist = Vector2.Distance(_core.Rb.position, _core.Player.position);

        if (currentDist <= DashStopDistance + 0.2f)
        {
            StopInFrontOfPlayer();
        }
        yield return StartCoroutine(DashFinishStrikeSequence());

        isDashing = false;
        _core.IsActing = false;
        _core._isHit = false;
        _core.AttackTimer = 0f;
        _core.SetPhysicsDuringAttack(false);
        Tutorial_Checker3 = true;
        ResetAnim();
    }

    private IEnumerator MeleeStrikeNoDash()
    {
        _core.Rb.linearVelocity = Vector2.zero;
        anim.SetTrigger("OneTwoReady");

        if (_curPreWindup > 0f)
            yield return new WaitForSeconds(_curPreWindup);

        yield return StartCoroutine(DashFinishStrikeSequence());

        _core.IsActing = false;
        _core._isHit = false;
        _core.AttackTimer = 0f;
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

            Vector2 dir = ((Vector2)_core.Player.position - _core.Rb.position).normalized;
            Vector2 nextPos = _core.Rb.position + dir * 0.5f;
            _core.Rb.position = _core.ClampInside(nextPos);

            yield return new WaitForSeconds(0.3f);
        }

        _core.IsActing = false;
        anim?.SetBool("Roll", false);
        _core._isHit = false;
        _core.AttackTimer = 0f;
        _core.SetPhysicsDuringAttack(false);
        ResetAnim();
    }

    public void Range_Attack()
    {
        if (_core.IsActing) return;
        var type = (EnemyCore_01.RangeAttackType)Random.Range(0, 3);
        Range_Attack(type);
    }

    public void Range_Attack(EnemyCore_01.RangeAttackType type)
    {
        if (_core.IsActing) return;
        switch (type)
        {
            case EnemyCore_01.RangeAttackType.Short:
                _desiredDistance = 5f;  _rangePreWindup = RangePreWindupShort; _volleyCount = VolleyCountShort; break;
            case EnemyCore_01.RangeAttackType.Mid:
                _desiredDistance = 7f;  _rangePreWindup = RangePreWindupMid;   _volleyCount = VolleyCountMid;   break;
            case EnemyCore_01.RangeAttackType.Long:
                _desiredDistance = 9f;  _rangePreWindup = RangePreWindupLong;  _volleyCount = VolleyCountLong;  break;
        }
        _core.IsActing = true;
        StartCoroutine(RetreatThenFire());
    }

    private IEnumerator DashFinishStrikeSequence()
    {
        if (_core.LastMeleeType == EnemyCore_01.MeleeAttackType.One)
        {
            yield return new WaitForSeconds(0.1f);
            anim.SetBool("One", true);
            _core.TriggerMeleeDamage();
            yield return new WaitForSeconds(0.5f);
            anim?.SetBool("One", false);
        }
        else if (_core.LastMeleeType == EnemyCore_01.MeleeAttackType.OneOne)
        {
            yield return new WaitForSeconds(0.1f);
            anim.SetBool("One", true);
            anim?.SetBool("OneOne", true);
            _core.TriggerMeleeDamage();
            yield return new WaitForSeconds(0.4f);
            _core.TriggerMeleeDamage();
            yield return new WaitForSeconds(0.5f);
            anim?.SetBool("OneOne", false);
            anim?.SetBool("One", false);
        }
        else if (_core.LastMeleeType == EnemyCore_01.MeleeAttackType.OneTwo)
        {
            yield return new WaitForSeconds(0.1f);
            anim?.SetBool("OneTwo", true);
            yield return new WaitForSeconds(0.1f);
            _core.TriggerMeleeDamage();
            yield return new WaitForSeconds(0.4f);
            _core.TriggerMeleeDamage();
            yield return new WaitForSeconds(0.5f);
            anim?.SetBool("OneTwo", false);
        }
        else
        {
            _core.TriggerMeleeDamage();
        }
    }

    private IEnumerator RetreatThenFire()
    {
        float t = 0f;
        while (t < RetreatDuration)
        {
            if (_core.Player == null || _core.Rb == null) break;

            Vector2 toPlayer = (Vector2)_core.Player.position - _core.Rb.position;
            float dist = toPlayer.magnitude;

            Vector2 dirAway = (-toPlayer).sqrMagnitude > 1e-8f ? (-toPlayer).normalized : Vector2.zero;
            float step = RetreatSpeed * Time.fixedDeltaTime;

            Vector2 nextPos = _core.Rb.position + dirAway * step;
            nextPos = _core.ClampInside(nextPos);
            _core.Rb.MovePosition(nextPos);

            if (_desiredDistance > 0f && dist >= _desiredDistance) break;

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
        _core._isHit = false;
        _core.AttackTimer = 0f;
        _core.ForceNextAction();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isDashing) return;
        if (!other.CompareTag("Player")) return;

        StartCoroutine(OnDashHitSequence());
    }

    private IEnumerator OnDashHitSequence()
    {
        isDashing = false;
        if (_core != null)
        {
            _core.Rb.linearVelocity = Vector2.zero;

            yield return StartCoroutine(DashFinishStrikeSequence());

            _core.IsActing = false;
        }
    }

    public void StopInFrontOfPlayer()
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
            case EnemyCore_01.AttackSelectMode.Range_Short:
                life = 1f;
                break;
            case EnemyCore_01.AttackSelectMode.Range_Mid:
                life = 2f;
                break;
            case EnemyCore_01.AttackSelectMode.Range_Long:
                life = 3f;
                break;
        }

        Destroy(proj, life);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_core == null) _core = GetComponent<EnemyCore_01>();
        if (_core == null) return;

        Vector3 pos = _core.Rb != null ? (Vector3)_core.Rb.position : transform.position;

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
        Gizmos.DrawWireSphere(pos, ChoiceDash);

        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.5f);
        Gizmos.DrawWireSphere(pos, DashStopDistance);
    }
    #endif
}
