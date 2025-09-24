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

    [Header("Melee Stop")]
    [SerializeField] private float StopOffset = 1.0f;
    [SerializeField] private LayerMask PlayerLayer;

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

    public void Melee_Attack()
    {
        if (_core.IsActing) return;

        int meleeType = Random.Range(0, 3);
        switch (meleeType)
        {
            case 0: _curPreWindup = PreWindupShort; break;
            case 1: _curPreWindup = PreWindupMid;   break;
            case 2: _curPreWindup = PreWindupLong;  break;
        }

        _core.IsActing = true;
        StartCoroutine(MeleeDash());
    }

    public void Range_Attack()
    {
        if (_core.IsActing) return;

        int rangeType = Random.Range(0, 3);
        switch (rangeType)
        {
            case 0:
                _desiredDistance = 5f;  _rangePreWindup = RangePreWindupShort; _volleyCount = VolleyCountShort; break;
            case 1:
                _desiredDistance = 7f;  _rangePreWindup = RangePreWindupMid;   _volleyCount = VolleyCountMid;   break;
            default:
                _desiredDistance = 9f;  _rangePreWindup = RangePreWindupLong;  _volleyCount = VolleyCountLong;  break;
        }

        _core.IsActing = true;
        StartCoroutine(RetreatThenFire());
    }

    private IEnumerator MeleeDash()
    {
        _core.Rb.linearVelocity = Vector2.zero;
        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.yellow;
        if (_curPreWindup > 0f) yield return new WaitForSeconds(_curPreWindup);

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

        if (_core.SpriteRenderer) _core.SpriteRenderer.color = Color.red;
        Vector2 startPos = _core.Rb.position;
        Vector2 lockedDir = ((Vector2)_core.Player.position - startPos).normalized;
        if (lockedDir.sqrMagnitude < 1e-8f) lockedDir = Vector2.right;

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
        yield return new WaitForSeconds(0.5f);

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

            Vector2 dirAway = (-toPlayer).normalized;
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

        Vector2 dir = ((Vector2)_core.Player.position - _core.Rb.position).normalized;
        Vector2 stopPos = (Vector2)_core.Player.position - dir * StopOffset;
        _core.Rb.position = _core.ClampInside(stopPos);
        _core.Rb.linearVelocity = Vector2.zero;
        _core.IsActing = false;
    }

    private void FireOneProjectile()
    {
        if (ProjectilePrefab && FirePoint)
            Instantiate(ProjectilePrefab, FirePoint.position, Quaternion.identity);
    }
}
