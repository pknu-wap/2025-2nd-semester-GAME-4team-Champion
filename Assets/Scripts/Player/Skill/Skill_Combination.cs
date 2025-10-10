using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skill_Combination : MonoBehaviour, IPlayerSkill
{
    [Header("Refs")]
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerAttack attack;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    [Header("Hitbox & Scaling")]
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private float damageMul = 1.5f;
    [SerializeField] private float knockMul = 1f;
    [SerializeField] private float rangeMul = 2f;
    [SerializeField] private float radiusMul = 1f;

    [Header("Timings (sec)")]
    [SerializeField] private float windup = 0.1f;
    [SerializeField] private float active = 0.06f;
    [SerializeField] private float recovery = 0.12f;
    [SerializeField] private bool zeroVelocityOnStart = true;

    [Header("Series (Combo Usage)")]
    [SerializeField] private int maxUsesPerSeries = 3;    // 최대 3연속
    [SerializeField] private float windowDuration = 3f;   // 마지막 사용 후 '연속창' 유지 시간
    [SerializeField] private float cooldownDuration = 8f; // 시리즈 종료 후 쿨타임

    [Header("Animation")]
    [SerializeField] private string animTriggerBase = "Combination";
    [SerializeField, Min(1)] private int variants = 3;

    [Header("VFX")]
    [SerializeField] private GameObject vfxPrefab;
    [SerializeField] private Transform vfxSpawnPoint;
    [SerializeField] private float vfxLifetime = 0.6f;
    [SerializeField] private float vfxFadeTime = 0.4f;

    // ==== TAG ====
    public const string TAG_COMBINATION_LAST = "Tag.Skill.Combination.Last";
    public event System.Action<string> OnTag;

    // 내부 상태
    private bool isCasting = false;
    private int usesInSeries = 0;
    private float windowEndTime = -999f;
    private float cooldownEndTime = -999f;
    private Coroutine windowRoutine;
    private bool tagLastThisCast;

    // 중복 타격 방지(재사용)
    private static readonly HashSet<int> _seenIds = new HashSet<int>(32);

    // IPlayerSkill 인터페이스
    public string SkillName => "Combination";
    public float GetTotalDuration() => windup + active + recovery;

    public bool TryCastSkill(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        if (owner) attack = owner;
        if (c) combat = c;
        if (m) moveRef = m;
        if (a) animator = a;
        return TryCast();
    }

    public bool TryCast()
    {
        if (isCasting) return false;
        if (Time.time < cooldownEndTime) return false;
        if (!combat || !attack || !moveRef || !animator) return false;

        // 시리즈 제한 검사
        if (usesInSeries >= maxUsesPerSeries) return false;

        // 이번 캐스트가 시리즈의 마지막인지 미리 기록
        tagLastThisCast = (usesInSeries == maxUsesPerSeries - 1);

        StartCoroutine(CastRoutine());
        return true;
    }

    private IEnumerator CastRoutine()
    {
        isCasting = true;

        combat.EnterCombat("Skill_Combination");
        combat.StartActionLock(GetTotalDuration(), zeroVelocityOnStart);

        // 애니 랜덤 트리거(즉시 중복 방지 X — 조합 다양성 유지)
        if (animator && variants > 0)
        {
            int idx = Mathf.Clamp(Random.Range(1, variants + 1), 1, variants);
            animator.SetTrigger($"{animTriggerBase}{idx}");
        }

        yield return new WaitForSeconds(windup);

        DoHitbox();
        yield return new WaitForSeconds(active + recovery);

        // 시리즈 관리
        usesInSeries++;
        windowEndTime = Time.time + windowDuration;
        if (windowRoutine != null) StopCoroutine(windowRoutine);
        windowRoutine = StartCoroutine(WindowWatch());

        isCasting = false;
    }

    private IEnumerator WindowWatch()
    {
        while (Time.time < windowEndTime)
            yield return null;

        // 창이 끊김 → 시리즈 초기화 + 쿨다운 시작
        usesInSeries = 0;
        cooldownEndTime = Time.time + cooldownDuration;
        windowRoutine = null;
    }

    private void DoHitbox()
    {
        var stats = (attack && attack.baseStats != null) ? attack.baseStats : new PlayerAttack.AttackBaseStats();
        float finalDamage = stats.baseDamage * damageMul;
        float finalKnock = stats.baseKnockback * knockMul;
        float finalRange = stats.baseRange * rangeMul;
        float finalRadius = stats.baseRadius * radiusMul;

        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 center = (Vector2)combat.transform.position + facing.normalized * finalRange;
        LayerMask mask = enemyMask.value != 0 ? enemyMask : combat.EnemyMask;

        var hits = Physics2D.OverlapCircleAll(center, finalRadius, mask);
        if (hits == null || hits.Length == 0) { SpawnVFX(); return; }

        _seenIds.Clear();
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h) continue;

            int rid = h.transform.root.GetInstanceID();
            if (!_seenIds.Add(rid)) continue;

            var dmgTarget = h.GetComponentInParent<IDamageable>();
            if (dmgTarget != null)
            {
                Vector2 dir = ((Vector2)h.transform.position - (Vector2)combat.transform.position).normalized;
                dmgTarget.ApplyHit(finalDamage, finalKnock, dir, gameObject);
            }
        }
        SpawnVFX();

        // 마지막 공격이었다면 태그 발행
        if (tagLastThisCast)
            OnTag?.Invoke(TAG_COMBINATION_LAST);
    }

    private void SpawnVFX()
    {
        if (!vfxPrefab) return;
        Transform t = vfxSpawnPoint ? vfxSpawnPoint : transform;
        var inst = Instantiate(vfxPrefab, t.position, t.rotation);
        Destroy(inst, vfxLifetime);
    }
}
