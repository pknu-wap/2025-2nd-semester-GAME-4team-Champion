using UnityEngine;
using System.Collections;

public class BossFight : MonoBehaviour
{
    [Header("Dash (Melee)")]
    [SerializeField] private float DashSpeed = 12f;
    [SerializeField] private float PreWindupShort = 2f;
    [SerializeField] private float PreWindupMid = 3f;
    [SerializeField] private float PreWindupLong = 1f;

    [Header("Melee Logic")]
    [SerializeField] public float NoDashCloseRange = 2.5f;
    [SerializeField] private float StopOffset = 1.0f;

    [Header("Melee Stop Settings")]
    [SerializeField] private float DashStopDistance = 1.15f;
    [SerializeField] private float MaxDashTime = 0.45f;
    [SerializeField] private bool LockDashDirection = false;

    [Header("Dash (Range)")]
    [SerializeField] private float RangePreWindupShort = 1.0f;
    [SerializeField] private float RangePreWindupMid = 1.3f;

    [Header("Post-Snap Control")]
    [SerializeField] private float SnapNoMoveDuration = 0.8f;

    private BossCore _core;
    private SpriteRenderer Sprite;
    private Animator anim;

    private float _curPreWindup = 2f;
    private float _rangePreWindup = 1.0f;

    public bool isDashing = false;
    public Transform player;

    //───────────────────────────────────────────────────────────────
    void Start()
    {
        Sprite = GetComponent<SpriteRenderer>();
        anim = GetComponentInChildren<Animator>();
        _core = GetComponent<BossCore>();
    }

    void Update()
    {
        if (player == null || _core == null) return;
        if (_core.IsDead()) return;

        Sprite.flipX = player.position.x < transform.position.x;
    }

    public void BindCore(BossCore core) => _core = core;

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

        int pick = Random.Range(0, 3);
        BossCore.MeleeAttackType attackType;
        switch (pick)
        {
            case 0:
                attackType = BossCore.MeleeAttackType.BossAttack1;
                break;
            case 1:
               attackType = BossCore.MeleeAttackType.BossAttack2;
                break;
            default:
                attackType = BossCore.MeleeAttackType.BossAttack3;
                break;
        }
        Melee_Attack(attackType);
    }

    public void Melee_Attack(BossCore.MeleeAttackType type)
    {
        if (_core == null || _core.IsActing) return;
        _core.IsActing = true;
        _core.SetPhysicsDuringAttack(true);
        _core.LastMeleeType = type;

        switch (type)
        {
            case BossCore.MeleeAttackType.BossAttack1: // 평타1 (후속타 2개 1개)
                _curPreWindup = PreWindupShort;
                StartCoroutine(MeleeBossAttack());
                break;
            case BossCore.MeleeAttackType.BossAttack2: // 엇박
                _curPreWindup = PreWindupMid;
                StartCoroutine(MeleeBossAttack());
                break;
            case BossCore.MeleeAttackType.BossAttack3: // 연타
                _curPreWindup = PreWindupLong;
                StartCoroutine(MeleeBossAttack());
                break;
        }
    }

    private IEnumerator MeleeBossAttack()
    {
        isDashing = true;
        _core.Rb.linearVelocity = Vector2.zero;
        anim.SetTrigger("SlamReady");

        if (_curPreWindup > 0f) yield return new WaitForSeconds(_curPreWindup);

        _core.Rb.linearVelocity = Vector2.zero;
        yield return StartCoroutine(MeleeDashFinishStrikeSequence());

        isDashing = false;
        _core.IsActing = false;
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
        var type = (BossCore.RangeAttackType)Random.Range(0, 3);
        Range_Attack(type);
    }

    public void Range_Attack(BossCore.RangeAttackType type)
    {
        if (_core == null || _core.IsActing) return;

        switch (type)
        {
            case BossCore.RangeAttackType.BossAttack1:
                _rangePreWindup = RangePreWindupShort;
                StartCoroutine(RangeBossAttack());
                break;
            case BossCore.RangeAttackType.BossAttack2:
                _rangePreWindup = RangePreWindupMid;
                StartCoroutine(RangeBossAttack());
                break;
        }

        _core.IsActing = true;
    }

    private IEnumerator RangeBossAttack()
    {
        isDashing = true;
        _core.Rb.linearVelocity = Vector2.zero;
        anim.SetTrigger("SlamReady");

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

        yield return StartCoroutine(RangeDashFinishStrikeSequence());

        isDashing = false;
        _core.IsActing = false;
        _core._isHit = false;
        _core.SetPhysicsDuringAttack(false);
        ResetAnim();
    }
    #endregion

    //───────────────────────────────────────────────────────────────
    #region Utility
    private IEnumerator MeleeDashFinishStrikeSequence()
    {
        switch (_core.LastMeleeType)
        {
            case BossCore.MeleeAttackType.BossAttack1: // 평타
                {
                    yield return StartCoroutine(BossAttack1Algorigm());
                    break;
                }

            case BossCore.MeleeAttackType.BossAttack2: // 엇박
                {
                    int MeleeMismatchedChoice = Random.Range(0, 2);
                    if (MeleeMismatchedChoice == 0) // 3
                    {
                        yield return new WaitForSeconds(0.1f);
                        anim.SetBool("Slam", true);
                        _core.TriggerMeleeDamage();
                        yield return new WaitForSeconds(0.3f);
                        anim.SetBool("Slam2", true);
                        _core.TriggerMeleeDamage();
                        yield return new WaitForSeconds(0.5f);
                    }
                    else // 없음
                    {
                        yield return new WaitForSeconds(0.1f);
                        anim.SetBool("Slam", true);
                        _core.TriggerMeleeDamage();
                        yield return new WaitForSeconds(0.5f);
                    }
                    break;
                }

            case BossCore.MeleeAttackType.BossAttack3: // 연타
                {
                    _core.TriggerMeleeDamage();
                    break;
                }
        }
    }
    
    private IEnumerator BossAttack1Algorigm()
    {
        int MeleeAttackChoice = Random.Range(0, 3); // 평타1
        if (MeleeAttackChoice == 0) // 2 3
        {
            yield return new WaitForSeconds(0.1f);
            anim.SetBool("Slam", true);
            _core.TriggerMeleeDamage();
            yield return new WaitForSeconds(0.4f);
            anim.SetBool("Slam2", true);
            _core.TriggerMeleeDamage();
            yield return new WaitForSeconds(1.0f);
            anim.SetBool("Slam3", true);
            _core.TriggerMeleeDamage();
            yield return new WaitForSeconds(0.5f);
        }
        else if (MeleeAttackChoice == 1) // 3
        {
            yield return new WaitForSeconds(0.1f);
            anim.SetBool("Slam", true);
            _core.TriggerMeleeDamage();
            yield return new WaitForSeconds(0.1f);
            anim.SetBool("Slam2", true);
            _core.TriggerMeleeDamage();
            yield return new WaitForSeconds(0.5f);
        }
        else // 없음
        {
            yield return new WaitForSeconds(0.1f);
            anim.SetBool("Slam", true);
            _core.TriggerMeleeDamage();
            yield return new WaitForSeconds(0.5f);
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

    private IEnumerator RangeDashFinishStrikeSequence()
    {
        switch (_core.LastRangeType)
        {
            case BossCore.RangeAttackType.BossAttack1: // 1
                yield return new WaitForSeconds(0.1f);
                anim.SetBool("Slam", true);
                _core.TriggerMeleeDamage();
                yield return new WaitForSeconds(0.5f);
                break;
            
            case BossCore.RangeAttackType.BossAttack2: // 2
                yield return new WaitForSeconds(0.1f);
                anim.SetBool("Slam", true);
                _core.TriggerMeleeDamage();
                yield return new WaitForSeconds(0.5f);
                break;
            case BossCore.RangeAttackType.BossAttack3: // 없음
                int AfterAttackChoice = Random.Range(0, 2);
                if (AfterAttackChoice == 0) // 평타1
                {
                    BossAttack1Algorigm();
                }
                break;
        }
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
        if (_core == null) _core = GetComponent<BossCore>();
        if (_core == null) return;

        Vector3 pos = _core.Rb != null ? (Vector3)_core.Rb.position : transform.position;

        Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
        Gizmos.DrawWireSphere(pos, NoDashCloseRange);

        Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.5f);
        Gizmos.DrawWireSphere(pos, DashStopDistance);
    }
#endif
}
