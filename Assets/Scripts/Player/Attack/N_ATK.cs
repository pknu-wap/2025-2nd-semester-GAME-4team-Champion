using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class N_ATK : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerAttack attack;
    [SerializeField] private PlayerAttack owner;
    [SerializeField] private PlayerDefense defense;
    [SerializeField] private PlayerHit hit;

    [Header("Combo Config")]
    [SerializeField] private int maxCombo = 4;
    [SerializeField] private float comboGapMax = 1f;
    [SerializeField] private bool lockMoveDuringAttack = true;

    [Header("Hitbox")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private bool ignoreEnemyCollisionDuringActive = true;
    [SerializeField] private float extraIgnoreTime = 0.02f;

    [Header("Timings (sec)")]
    [SerializeField] private float windup = 0.15f;
    [SerializeField] private float active = 0.06f;
    [SerializeField] private float recovery = 0.12f;
    [SerializeField] private float minRecovery = 0.05f;


    // state
    private int comboIndex = 0;
    private bool isAttacking = false;
    private bool nextBuffered = false;
    private float lastAttackEndTime = -999f;
    private Coroutine attackCo;
    private Coroutine attackMoveLockCo;
    private Collider2D[] myCols;

    public PlayerDefense Defense => defense;
    public PlayerHit Hit => hit;
    public bool IsStaminaBroken => defense != null && defense.IsStaminaBroken;
    public bool IsParryLocked => defense != null && defense.IsParryLocked;
    public bool InHitstun => hit != null && hit.InHitstun;

    public bool IsAttacking => isAttacking;
    private void Awake()
    {
        if (!owner) owner = GetComponent<PlayerAttack>(); // 같은 오브젝트면 자동 참조
        if (!defense) defense = GetComponent<PlayerDefense>();
        if (!hit) hit = GetComponent<PlayerHit>();
    }

    public void Bind(PlayerAttack atk, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        attack = atk;
        combat = c; moveRef = m; animator = a;
        myCols = GetComponents<Collider2D>();
    }

    public void OnAttackStarted()
    {
        if (combat != null && (defense.IsStaminaBroken || hit.InHitstun || defense.IsParryLocked)) return;

        if (isAttacking)
        {
            nextBuffered = true;
        }
        else
        {
            if (Time.time - lastAttackEndTime > comboGapMax) comboIndex = 0;
            attackCo = StartCoroutine(DoAttackStep(comboIndex));
        }
    }

    public void OnAttackCanceled() { /* 콤보는 취소 동작 없음 */ }

    private IEnumerator DoAttackStep(int step)
    {
        isAttacking = true;
        nextBuffered = false;

        animator?.SetTrigger($"Atk{Mathf.Clamp(step + 1, 1, maxCombo)}");

        if (lockMoveDuringAttack)
        {
            float lockTime = windup + active + recovery;
            if (attackMoveLockCo != null) StopCoroutine(attackMoveLockCo);
            attackMoveLockCo = StartCoroutine(LockMoveFor(lockTime, true));
        }

        yield return new WaitForSeconds(windup);

        // ★ 여기서만 attack.baseStats를 읽어 사용 (런타임 변경 반영)
        DoHitbox(attack.baseStats.baseDamage,
                 attack.baseStats.baseKnockback,
                 attack.baseStats.baseRange,
                 attack.baseStats.baseRadius);

        yield return new WaitForSeconds(active);

        // 최소 후딜 + 캔슬 가능 후딜
        float t = 0f;
        while (t < minRecovery) { t += Time.deltaTime; yield return null; }
        while (t < recovery)
        {
            if (nextBuffered && step < maxCombo - 1) break;
            t += Time.deltaTime;
            yield return null;
        }

        isAttacking = false;
        lastAttackEndTime = Time.time;

        if (nextBuffered && step < maxCombo - 1)
        {
            comboIndex = step + 1;
            attackCo = StartCoroutine(DoAttackStep(comboIndex));
        }
        else
        {
            comboIndex = 0;
            nextBuffered = false;
        }
        attackCo = null;
    }

    private IEnumerator LockMoveFor(float seconds, bool zeroVelocity = true)
    {
        moveRef?.SetMovementLocked(true, false, zeroVelocity);
        yield return new WaitForSeconds(seconds);
        moveRef?.SetMovementLocked(false, false);
        attackMoveLockCo = null;
        float lockTime = windup + active + recovery;
        combat?.BlockStaminaRegenFor(lockTime);
    }

    private void DoHitbox(float dmg, float knock, float range, float radius)
    {
        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f)
            ? moveRef.LastFacing : Vector2.right;
        Vector2 center = (Vector2)transform.position + facing.normalized * range;

        var hits = Physics2D.OverlapCircleAll(center, radius, enemyMask);
        var seen = new HashSet<Collider2D>();
        bool any = false;

        foreach (var h in hits)
        {
            if (!h || seen.Contains(h)) continue;
            seen.Add(h);

            if (ignoreEnemyCollisionDuringActive) IgnoreForSeconds(h.transform.root, active + extraIgnoreTime);

            Vector2 toEnemy = ((Vector2)h.transform.position - (Vector2)transform.position).normalized;
            var dmgTarget = h.GetComponentInParent<IDamageable>();
            if (dmgTarget != null)
            {
                dmgTarget.ApplyHit(dmg, knock, toEnemy, gameObject);
                any = true;
            }
            else { any = true; }
        }

        if (any) combat?.EnterCombat("HitEnemy");
    }

    private void IgnoreForSeconds(Transform enemyRoot, float seconds)
    {
        if (myCols == null) myCols = GetComponents<Collider2D>();
        var enemyCols = enemyRoot.GetComponentsInChildren<Collider2D>();
        foreach (var my in myCols)
        {
            if (!my) continue;
            foreach (var ec in enemyCols)
            {
                if (!ec) continue;
                Physics2D.IgnoreCollision(my, ec, true);
                StartCoroutine(ReenableLater(my, ec, seconds));
            }
        }
    }

    private IEnumerator ReenableLater(Collider2D a, Collider2D b, float t)
    {
        yield return new WaitForSeconds(t);
        if (a && b) Physics2D.IgnoreCollision(a, b, false);
    }
}
