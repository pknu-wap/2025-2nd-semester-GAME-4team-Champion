using UnityEngine;
using System.Collections;

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
    [SerializeField] private float NoDashCloseRange = 2.5f;   // 이 이내면 OneTwo, 밖이면 One/OneOne
    [SerializeField] private float StopOffset = 1.0f;

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

    private BossCore _core;

    private float _curPreWindup = 2f;
    private float _desiredDistance = 7f;
    private int _volleyCount = 1;
    private float _rangePreWindup = 1.0f;

    public void BindCore(BossCore core) => _core = core;

    // ===========================
    // 근거리: 거리 기반 자동 분기
    // dist > NoDashCloseRange → One/OneOne 랜덤(돌진)
    // dist <= NoDashCloseRange → OneTwo(완전 무이동)
    // ===========================
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

    // 호환용(기존 호출 지점이 있을 수 있음)
    public void Melee_Attack()
    {
        if (_core.IsActing) return;
        Melee_Attack_DistanceBased();
    }

    // 선택형: 타입에 맞는 연출 실행
    public void Melee_Attack(BossCore.MeleeAttackType type)
    {
        if (_core.IsActing) return;

        switch (type)
        {
            case BossCore.MeleeAttackType.One:
                _curPreWindup = PreWindupShort;
                _core.LastMeleeType = BossCore.MeleeAttackType.One;
                _core.IsActing = true;
                StartCoroutine(MeleeDash()); // 돌진
                break;

            case BossCore.MeleeAttackType.OneOne:
                _curPreWindup = PreWindupMid;
                _core.LastMeleeType = BossCore.MeleeAttackType.OneOne;
                _core.IsActing = true;
                StartCoroutine(MeleeDash()); // 돌진
                break;

            case BossCore.MeleeAttackType.OneTwo:
                _curPreWindup = PreWindupLong;
                _core.LastMeleeType = BossCore.MeleeAttackType.OneTwo;
                _core.IsActing = true;
                // 접근/스냅 없이 정지 상태에서 타격
                StartCoroutine(MeleeStrikeNoDash(closeUpIfFar:false, snapInFront:false));
                break;
        }
    }

    // ===========================
    // 원거리 공격(랜덤/선택)
    // ===========================
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

    // ===========================
    // 근거리: 돌진 패턴 (One/OneOne)
    // ===========================
    private IEnumerator MeleeDash()
    {
        _core.Rb.linearVelocity = Vector2.zero;
        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.yellow;
        if (_curPreWindup > 0f) yield return new WaitForSeconds(_curPreWindup);

        // 슬로우 접근
        float t = 0f;
        while (t < PreDashSlowDuration)
        {
            if (_core.Player == null) break;
            Vector2 toPlayer = (Vector2)_core.Player.position - _core.Rb.position;
            Vector2 dirSlow = toPlayer.sqrMagnitude > 1e-8f ? toPlayer.normalized : Vector2.zero;
            _core.Rb.linearVelocity = dirSlow * SlowApproachSpeed;
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        _core.Rb.linearVelocity = Vector2.zero;

        // 본 돌진
        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.red;
        Vector2 startPos = _core.Rb.position;
        Vector2 toP = (_core.Player != null) ? ((Vector2)_core.Player.position - startPos) : Vector2.right;
        Vector2 lockedDir = toP.sqrMagnitude > 1e-8f ? toP.normalized : Vector2.right;

        float dashTime = 0.3f;
        float elapsed = 0f;
        while (elapsed < dashTime)
        {
            _core.Rb.linearVelocity = lockedDir * DashSpeed;
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        _core.Rb.linearVelocity = Vector2.zero;
        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.white;

        // 타이밍에 히트 윈도우 열기
        _core.TriggerMeleeDamage();

        // 약간의 후딜
        yield return new WaitForSeconds(0.5f);

        _core.IsActing = false;
    }

    // ===========================
    // 근거리: 무돌진 근접 타격 (OneTwo)
    // ===========================
    private IEnumerator MeleeStrikeNoDash(bool closeUpIfFar, bool snapInFront)
    {
        _core.Rb.linearVelocity = Vector2.zero;
        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.yellow; // 안전하게 yellow 대체
        if (_curPreWindup > 0f) yield return new WaitForSeconds(_curPreWindup);

        // closeUpIfFar=false, snapInFront=false → 완전 무이동
        _core.Rb.linearVelocity = Vector2.zero;

        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.red;

        // 히트 윈도우 열기
        _core.TriggerMeleeDamage();

        yield return new WaitForSeconds(0.35f);

        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.white;
        _core.IsActing = false;
    }

    // ===========================
    // 원거리: 후퇴 후 발사
    // ===========================
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

        Gizmos.color = new Color(255f, 0f, 0f, 255f);
        Vector3 pos = _core.Rb != null ? (Vector3)_core.Rb.position : transform.position;
        Gizmos.DrawWireSphere(pos, NoDashCloseRange);
    }
#endif
}
