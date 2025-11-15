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
    [SerializeField] private int maxUsesPerSeries = 3;    // 3연속
    [SerializeField] private float windowDuration = 3f;   // 연속창
    [SerializeField] private float cooldownDuration = 8f; // 시리즈 쿨

    [Header("Animation")]
    [SerializeField] private string animTriggerBase = "Combination";

    [Header("VFX (단계별)")]
    [SerializeField] private GameObject[] stepVFX = new GameObject[3]; // 0/1/2 = 1/2/3타
    [SerializeField] private Vector2 vfxOffset = new Vector2(0.7f, 0f);
    [SerializeField] private bool vfxAttachToPlayer = true;
    [SerializeField] private float vfxStartDelay = 0f; // 🔸 시작 타이밍
    [SerializeField] private float vfxFadeIn = 0.03f;  // 🔸 인
    [SerializeField] private float vfxHold = 0.0f;     // 유지
    [SerializeField] private float vfxFadeOut = 0.35f; // 🔸 아웃

    // ==== TAG ====
    public const string TAG_COMBINATION_LAST = "Tag.Skill.Combination.Last";
    public event System.Action<string> OnTag;

    private bool isCasting = false;
    private int usesInSeries = 0; // 0→1→2
    private float windowEndTime = -999f;
    private float cooldownEndTime = -999f;
    private Coroutine windowRoutine;
    private bool tagLastThisCast;

    private static readonly HashSet<int> _seenIds = new HashSet<int>(32);
    private SpriteRenderer _cachedPlayerSR;
    private SpriteRenderer PlayerSR => _cachedPlayerSR ??= combat.GetComponentInChildren<SpriteRenderer>();

    public string SkillName => "Combination";
    public float GetTotalDuration() => windup + active + recovery;

    public bool TryCastSkill(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    { if (owner) attack = owner; if (c) combat = c; if (m) moveRef = m; if (a) animator = a; return TryCast(); }

    public bool TryCast()
    {
        if (isCasting) return false;
        if (Time.time < cooldownEndTime) return false;
        if (!combat || !attack || !moveRef || !animator) return false;
        if (usesInSeries >= maxUsesPerSeries) return false;

        tagLastThisCast = (usesInSeries == maxUsesPerSeries - 1);
        StartCoroutine(CastRoutine());
        return true;
    }

    public void OnPlayerHitInterrupt(PlayerHit.HitInterruptInfo info)
    {
        if (!isCasting) return;
        // 현재 캐스트 중단 (시리즈 카운트는 증가시키지 않음)
        StopAllCoroutines();
        isCasting = false;
    }

    private IEnumerator CastRoutine()
    {
        isCasting = true;
        TagBus.Raise("Tag.Zoom");
        combat.EnterCombat("Skill_Combination");
        combat.StartActionLock(GetTotalDuration(), zeroVelocityOnStart);

        int stepIndex = Mathf.Clamp(usesInSeries, 0, maxUsesPerSeries - 1); // 0,1,2
        animator?.SetTrigger($"{animTriggerBase}{stepIndex + 1}");

        yield return new WaitForSeconds(windup);

        DoHitbox();
        TagBus.Raise("Tag.impact(L)");
        // 단계별 VFX (딜레이+페이드)
        StartCoroutine(SpawnStepVFXWithFade(stepIndex));

        yield return new WaitForSeconds(active + recovery);

        usesInSeries++;
        windowEndTime = Time.time + windowDuration;
        if (windowRoutine != null) StopCoroutine(windowRoutine);
        windowRoutine = StartCoroutine(WindowWatch());

        isCasting = false;
    }

    private IEnumerator WindowWatch()
    {
        while (Time.time < windowEndTime) yield return null;
        usesInSeries = 0;
        cooldownEndTime = Time.time + cooldownDuration;
        windowRoutine = null;
    }

    private void DoHitbox()
    {
        var stats = (attack && attack.baseStats != null) ? attack.baseStats : new PlayerAttack.AttackBaseStats();
        float dmg = stats.baseDamage * damageMul;
        float range = stats.baseRange * rangeMul;
        float radius = stats.baseRadius * radiusMul;

        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 center = (Vector2)combat.transform.position + facing.normalized * range;
        LayerMask mask = enemyMask.value != 0 ? enemyMask : combat.EnemyMask;

        var hits = Physics2D.OverlapCircleAll(center, radius, mask);
        if (hits == null || hits.Length == 0) { return; }

        _seenIds.Clear();
        foreach (var h in hits)
        {
            if (!h) continue;
            int rid = h.transform.root.GetInstanceID();
            if (!_seenIds.Add(rid)) continue;

            var dmgTarget = h.GetComponentInParent<IDamageable>();
            if (dmgTarget != null)
            {
                Vector2 dir = ((Vector2)h.transform.position - (Vector2)combat.transform.position).normalized;
                dmgTarget.ApplyHit(dmg, dir, gameObject);
            }
        }

        if (tagLastThisCast) OnTag?.Invoke(TAG_COMBINATION_LAST);
        TagBus.Raise("Tag.Skill.Combination.Last");
    }

    private IEnumerator SpawnStepVFXWithFade(int stepIndex)
    {
        if (stepVFX == null || stepVFX.Length == 0) yield break;
        int i = Mathf.Clamp(stepIndex, 0, stepVFX.Length - 1);
        var prefab = stepVFX[i]; if (!prefab || !combat) yield break;

        if (vfxStartDelay > 0f) yield return new WaitForSeconds(vfxStartDelay);

        var go = Instantiate(prefab, combat.transform.position, Quaternion.identity);
        var follower = go.GetComponent<VFXFollowFlip>() ?? go.AddComponent<VFXFollowFlip>();
        follower.Init(combat.transform, PlayerSR, vfxOffset, vfxAttachToPlayer);

        var sr = go.GetComponentInChildren<SpriteRenderer>();
        var cg = go.GetComponentInChildren<CanvasGroup>();

        if (sr != null)
        {
            Color c = sr.color;
            if (vfxFadeIn > 0f) { float t = 0f; while (t < vfxFadeIn) { sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0f, 1f, t / vfxFadeIn)); t += Time.deltaTime; yield return null; } sr.color = new Color(c.r, c.g, c.b, 1f); }
            if (vfxHold > 0f) yield return new WaitForSeconds(vfxHold);
            if (vfxFadeOut > 0f) { float t = 0f; while (t < vfxFadeOut) { sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, t / vfxFadeOut)); t += Time.deltaTime; yield return null; } }
            Destroy(go); yield break;
        }
        if (cg != null)
        {
            if (vfxFadeIn > 0f) { float t = 0f; while (t < vfxFadeIn) { cg.alpha = Mathf.Lerp(0f, 1f, t / vfxFadeIn); t += Time.deltaTime; yield return null; } cg.alpha = 1f; }
            if (vfxHold > 0f) yield return new WaitForSeconds(vfxHold);
            if (vfxFadeOut > 0f) { float t = 0f; while (t < vfxFadeOut) { cg.alpha = Mathf.Lerp(1f, 0f, t / vfxFadeOut); t += Time.deltaTime; yield return null; } }
            Destroy(go); yield break;
        }

        // 렌더러 없으면 타임아웃
        float total = Mathf.Max(0f, vfxFadeIn + vfxHold + vfxFadeOut);
        yield return new WaitForSeconds(total);
        Destroy(go);
    }
}
