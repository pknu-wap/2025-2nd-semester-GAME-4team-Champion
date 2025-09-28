using UnityEngine;
using System.Collections;

public class EnemyFight_01 : MonoBehaviour
{
    [Header("Dash (Melee)")]
    [SerializeField] private float DashSpeed = 12f; 
    [SerializeField] private float PreWindupShort = 2f; 
    [SerializeField] private float PreWindupMid = 3f; 
    [SerializeField] private float PreWindupLong = 4f; 
    [SerializeField] private float PreWindupRoll = 0.7f; 

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

    private EnemyCore_01 _core;
    private SpriteRenderer Sprite;

    private float _curPreWindup = 2f;
    private float _desiredDistance = 7f;
    private int _volleyCount = 1;
    private float _rangePreWindup = 1.0f;

    public bool isDashing = false;

    void Start()
    {
        Sprite = GetComponent<SpriteRenderer>();
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

    // 거리 기반 자동 분기
    public void Melee_Attack_DistanceBased()
    {
        if (_core.IsActing) return;

        float dist = (_core.Player != null)
            ? Vector2.Distance(_core.Rb.position, (Vector2)_core.Player.position)
            : Mathf.Infinity;

        if (dist > NoDashCloseRange)
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

    public void Melee_Attack()
    {
        if (_core.IsActing) return;
        Melee_Attack_DistanceBased();
    }

    public void Melee_Attack(EnemyCore_01.MeleeAttackType type)
    {
        if (_core.IsActing) return;
        Sprite.color = Color.red;

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

    // 대쉬 패턴
    private IEnumerator MeleeDash()
    {
        isDashing = true;
        _core.Rb.linearVelocity = Vector2.zero;
        Sprite.color = Color.yellow;

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
        StopInFrontOfPlayer();

        Sprite.color = Color.blue;
        yield return StartCoroutine(DashFinishStrikeSequence());

        Sprite.color = Color.red;
        isDashing = false;
        _core.IsActing = false;
    }

    private IEnumerator MeleeStrikeNoDash()
    {
        _core.Rb.linearVelocity = Vector2.zero;
        Sprite.color = Color.yellow;

        if (_curPreWindup > 0f) yield return new WaitForSeconds(_curPreWindup);

        Sprite.color = Color.blue;
        _core.TriggerMeleeDamage();

        yield return new WaitForSeconds(0.35f);

        Sprite.color = Color.red;
        _core.IsActing = false;
    }

    private IEnumerator MeleeStrikeRoll()
    {
        _core.Rb.linearVelocity = Vector2.zero;
        Sprite.color = Color.yellow;

        if (_curPreWindup > 0f)
            yield return new WaitForSeconds(_curPreWindup);

        Sprite.color = Color.blue;

        for (int i = 0; i < 10; i++)
        {
            _core.TriggerMeleeDamage();

            if (_core.Player != null)
            {
                Vector2 dir = ((Vector2)_core.Player.position - _core.Rb.position).normalized;
                Vector2 nextPos = _core.Rb.position + dir * 0.5f;
                _core.Rb.position = _core.ClampInside(nextPos);
            }

            yield return new WaitForSeconds(0.5f);
        }

        Sprite.color = Color.red;
        _core.IsActing = false;
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
        if (_core == null) yield break;

        _core.TriggerMeleeDamage();

        if (_core.LastMeleeType == EnemyCore_01.MeleeAttackType.OneOne)
        {
            yield return new WaitForSeconds(0.5f);
            _core.TriggerMeleeDamage();
        }
    }

    private IEnumerator RetreatThenFire()
    {
        Sprite.color = Color.green;

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

        Sprite.color = Color.blue;
        for (int i = 0; i < _volleyCount; i++)
        {
            FireOneProjectile();
            if (i < _volleyCount - 1 && VolleyInterval > 0f)
                yield return new WaitForSeconds(VolleyInterval);
        }

        Sprite.color = Color.red;
        _core.IsActing = false;
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
            Sprite.color = Color.blue;

            yield return StartCoroutine(DashFinishStrikeSequence());

            Sprite.color = Color.red;
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
        if (ProjectilePrefab && FirePoint)
            Object.Instantiate(ProjectilePrefab, FirePoint.position, Quaternion.identity);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_core == null) _core = GetComponent<EnemyCore_01>();
        if (_core == null) return;

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
        Vector3 pos = _core.Rb != null ? (Vector3)_core.Rb.position : transform.position;
        Gizmos.DrawWireSphere(pos, NoDashCloseRange);
    }
#endif
}
