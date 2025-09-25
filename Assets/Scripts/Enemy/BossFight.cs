using System.Collections;
using UnityEngine;

public class BossFight : MonoBehaviour
{
    [Header("Dash (Melee)")]
    [SerializeField] private float DashSpeed = 12f;
    [SerializeField] private float SlowApproachSpeed = 1.5f;
    [SerializeField] private float PreDashSlowDuration = 0.5f;
    [SerializeField] private float PreWindupShort = 2f;
    [SerializeField] private float PreWindupMid = 3f;
    [SerializeField] private float PreWindupLong = 4f;

    [Header("Melee Logic")]
    [SerializeField] private float NoDashCloseRange = 2.5f;   // dist > 값 → One/OneOne, dist <= 값 → OneTwo
    [SerializeField] private float StopOffset = 1.0f;         // 최종적으로 플레이어 앞에서 유지할 간격

    [Header("Melee Stop Settings")]
    [SerializeField] private float DashStopDistance = 1.15f;  // 이 거리 이하면 대쉬 종료
    [SerializeField] private float MaxDashTime = 0.45f;       // 대쉬 최대 시간
    [SerializeField] private bool LockDashDirection = false;  // true=대쉬 시작 방향 고정, false=추적

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
    [SerializeField] private float SnapNoMoveDuration = 0.8f; // Snap 후 이동 금지 시간

    private BossCore _core;

    private float _curPreWindup = 2f;
    private float _desiredDistance = 7f;
    private int _volleyCount = 1;
    private float _rangePreWindup = 1.0f;

    private bool _isDashing = false;
    public bool IsDashing => _isDashing;

    public void BindCore(BossCore core) => _core = core;

    // 외부(패링 등)에서 호출
    public void InterruptDash()
    {
        _isDashing = false;
        if (_core != null)
        {
            _core.Rb.linearVelocity = Vector2.zero;
            _core.IsActing = false;
            _core.ForceNextAction();
        }
    }

    // 옆으로 순간이동 (Snap)
    public void SnapBesidePlayer(float sideOffset)
    {
        if (_core == null || _core.Player == null || _core.Rb == null) return;

        Vector2 playerPos = _core.Player.position;
        Vector2 bossPos   = _core.Rb.position;
        Vector2 toBoss    = bossPos - playerPos;
        if (toBoss.sqrMagnitude < 1e-6f) toBoss = Vector2.right;

        Vector2 perp = new Vector2(-toBoss.y, toBoss.x).normalized;
        float sideSign = Mathf.Sign(Vector2.Dot(toBoss, perp));
        Vector2 sideDir = perp * sideSign;

        Vector2 target = playerPos + sideDir * Mathf.Max(0.05f, sideOffset);
        _core.Rb.position = _core.ClampInside(target);
        _core.Rb.linearVelocity = Vector2.zero;

        _core.StartNoMoveCooldown(SnapNoMoveDuration);
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
            Melee_Attack(pick == 0 ? BossCore.MeleeAttackType.One
                                   : BossCore.MeleeAttackType.OneOne);
        }
        else
        {
            Melee_Attack(BossCore.MeleeAttackType.OneTwo);
        }
    }

    public void Melee_Attack()
    {
        if (_core.IsActing) return;
        Melee_Attack_DistanceBased();
    }

    public void Melee_Attack(BossCore.MeleeAttackType type)
    {
        if (_core.IsActing) return;
        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.red;

        switch (type)
        {
            case BossCore.MeleeAttackType.One:
                _curPreWindup = PreWindupShort;
                _core.LastMeleeType = BossCore.MeleeAttackType.One;
                _core.IsActing = true;
                StartCoroutine(MeleeDash());
                break;
            case BossCore.MeleeAttackType.OneOne:
                _curPreWindup = PreWindupMid;
                _core.LastMeleeType = BossCore.MeleeAttackType.OneOne;
                _core.IsActing = true;
                StartCoroutine(MeleeDash());
                break;
            case BossCore.MeleeAttackType.OneTwo:
                _curPreWindup = PreWindupLong;
                _core.LastMeleeType = BossCore.MeleeAttackType.OneTwo;
                _core.IsActing = true;
                StartCoroutine(MeleeStrikeNoDash());
                break;
        }
    }

    public void Range_Attack()
    {
        if (_core.IsActing) return;
        var type = (BossCore.RangeAttackType)Random.Range(0, 3);
        Range_Attack(type);
    }

    public void Range_Attack(BossCore.RangeAttackType type)
    {
        if (_core.IsActing) return;
        switch (type)
        {
            case BossCore.RangeAttackType.Short:
                _desiredDistance = 5f;  _rangePreWindup = RangePreWindupShort; _volleyCount = VolleyCountShort; break;
            case BossCore.RangeAttackType.Mid:
                _desiredDistance = 7f;  _rangePreWindup = RangePreWindupMid;   _volleyCount = VolleyCountMid;   break;
            case BossCore.RangeAttackType.Long:
                _desiredDistance = 9f;  _rangePreWindup = RangePreWindupLong;  _volleyCount = VolleyCountLong;  break;
        }
        _core.IsActing = true;
        StartCoroutine(RetreatThenFire());
    }

    // 대쉬 후 타격 시퀀스
    private IEnumerator DashFinishStrikeSequence()
    {
        if (_core == null) yield break;

        _core.TriggerMeleeDamage();

        if (_core.LastMeleeType == BossCore.MeleeAttackType.OneOne)
        {
            yield return new WaitForSeconds(0.5f);
            _core.TriggerMeleeDamage();
        }
    }

    // 대쉬 패턴
    private IEnumerator MeleeDash()
    {
        _isDashing = true;
        _core.Rb.linearVelocity = Vector2.zero;
        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.yellow;

        if (_curPreWindup > 0f) yield return new WaitForSeconds(_curPreWindup);

        Vector2 lockedDir = Vector2.right;
        if (_core.Player != null)
        {
            Vector2 toP0 = (Vector2)_core.Player.position - _core.Rb.position;
            lockedDir = toP0.sqrMagnitude > 1e-8f ? toP0.normalized : Vector2.right;
        }

        float elapsed = 0f;
        while (_isDashing)
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

        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.blue;
        yield return StartCoroutine(DashFinishStrikeSequence());

        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.red;
        _isDashing = false;
        _core.IsActing = false;
    }

    private IEnumerator MeleeStrikeNoDash()
    {
        _core.Rb.linearVelocity = Vector2.zero;
        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.yellow;

        if (_curPreWindup > 0f) yield return new WaitForSeconds(_curPreWindup);

        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.blue;
        _core.TriggerMeleeDamage();

        yield return new WaitForSeconds(0.35f);

        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.red;
        _core.IsActing = false;
    }

    private IEnumerator RetreatThenFire()
    {
        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.green;

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

        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.blue;
        for (int i = 0; i < _volleyCount; i++)
        {
            FireOneProjectile();
            if (i < _volleyCount - 1 && VolleyInterval > 0f)
                yield return new WaitForSeconds(VolleyInterval);
        }

        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.red;
        _core.IsActing = false;
        _core.ForceNextAction();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsDashing) return;
        if (!other.CompareTag("Player")) return;

        StartCoroutine(OnDashHitSequence());
    }

    private IEnumerator OnDashHitSequence()
    {
        _isDashing = false;
        if (_core != null)
        {
            _core.Rb.linearVelocity = Vector2.zero;
            if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.blue;

            yield return StartCoroutine(DashFinishStrikeSequence());

            if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.red;
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
        if (_core == null) _core = GetComponent<BossCore>();
        if (_core == null) return;

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
        Vector3 pos = _core.Rb != null ? (Vector3)_core.Rb.position : transform.position;
        Gizmos.DrawWireSphere(pos, NoDashCloseRange);
    }
#endif
}
