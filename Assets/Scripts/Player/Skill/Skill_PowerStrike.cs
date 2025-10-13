using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skill_PowerStrike : MonoBehaviour, IPlayerSkill
{
    [Header("Refs (선택)")]
    [SerializeField] private PlayerAttack attack;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    [Header("Timing (sec)")]
    [SerializeField] private float windup = 0.06f;
    [SerializeField] private float active = 0.06f;
    [SerializeField] private float recovery = 0.30f;
    public float GetTotalDuration() => windup + active + recovery;

    [Header("Skill Config")]
    [SerializeField, Min(1f)] private float damageMul = 3f;
    [SerializeField] private float knockMul = 1.5f;
    [SerializeField] private float rangeMul = 1.0f;
    [SerializeField] private float radiusMul = 1.0f;
    [SerializeField] private string triggerName = "PowerStrike";
    [SerializeField] private bool lockMoveDuringSkill = true;

    [Header("Cooldown")]
    [SerializeField] private float cooldownSeconds = 8f;
    private float lastCastEndTime = -999f;
    public float CooldownRemain => Mathf.Max(0f, (lastCastEndTime + cooldownSeconds) - Time.time);
    public bool IsOnCooldown => CooldownRemain > 0f;

    [Header("VFX")]
    [SerializeField] private float vfxScaleMul = 1f;
    [SerializeField] private GameObject vfxPrefab;
    [SerializeField] private Vector2 vfxOffset = new Vector2(0.6f, 0f);
    [SerializeField] private bool attachToPlayer = true;
    [SerializeField] private float vfxStartDelay = 0f;   // 🔸 시작 딜레이
    [SerializeField] private float vfxFadeIn = 0.05f;    // 🔸 페이드 인
    [SerializeField] private float vfxHold = 0.0f;       // 유지 시간(옵션)
    [SerializeField] private float vfxFadeOut = 0.5f;    // 🔸 페이드 아웃

    // ==== TAG ====
    public const string TAG_POWERSTRIKE_CAST = "Tag.Skill.PowerStrike.Cast";
    public event System.Action<string> OnTag;

    private static readonly HashSet<int> _seenIds = new HashSet<int>(32);
    private Coroutine castCo;
    private SpriteRenderer _cachedPlayerSR;
    private SpriteRenderer PlayerSR => _cachedPlayerSR ??= combat.GetComponentInChildren<SpriteRenderer>();

    private void Reset()
    {
        if (!attack) attack = GetComponentInParent<PlayerAttack>();
        if (!combat) combat = GetComponentInParent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponentInParent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponentInParent<Animator>();
    }

    public string SkillName => "PowerStrike";

    public bool TryCastSkill(PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    { combat = c; moveRef = m; animator = a; return TryCastInternal(); }

    public bool TryCastSkill(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    { attack = owner; combat = c; moveRef = m; animator = a; return TryCastInternal(); }

    private bool TryCastInternal()
    {
        if (!combat || !moveRef || !animator) return false;
        if (IsOnCooldown) return false;
        if (combat.HP <= 0f) return false;
        if (combat.IsActionLocked || combat.IsStaminaBroken) return false;

        if (castCo != null) StopCoroutine(castCo);
        castCo = StartCoroutine(CastRoutine());
        return true;
    }

    private bool isCasting;

    public void OnPlayerHitInterrupt(PlayerHit.HitInterruptInfo info)
    {
        if (!isCasting) return;
        if (castCo != null) { StopCoroutine(castCo); castCo = null; }
        isCasting = false;
        animator?.ResetTrigger(triggerName);
        moveRef?.RemoveMovementLock("LOCK_POWERSTRIKE", false);
    }

    private IEnumerator CastRoutine()
    {
        float total = windup + active + recovery;
        if (lockMoveDuringSkill) combat.StartActionLock(total, true);

        // VFX (시작 타이밍과 페이드 제어)
        StartCoroutine(SpawnVFXWithFollowAndFade(vfxPrefab, vfxOffset, attachToPlayer,
                                                 vfxStartDelay, vfxFadeIn, vfxHold, vfxFadeOut));

        // 선딜
        yield return new WaitForSeconds(windup);

        if (!string.IsNullOrEmpty(triggerName)) animator.SetTrigger(triggerName);

        OnTag?.Invoke(TAG_POWERSTRIKE_CAST);

        var stats = (attack != null && attack.baseStats != null) ? attack.baseStats : new PlayerAttack.AttackBaseStats();
        float dmg = stats.baseDamage * damageMul;
        float knock = stats.baseKnockback * knockMul;
        float range = stats.baseRange * rangeMul;
        float radius = stats.baseRadius * radiusMul;

        DoHitbox(dmg, knock, range, radius);

        yield return new WaitForSeconds(active + recovery);
        animator.SetBool("immune", false);
        combat.EnterCombat("Skill_PowerStrike");
        lastCastEndTime = Time.time;
        castCo = null;
    }

    private IEnumerator SpawnVFXWithFollowAndFade(GameObject prefab, Vector2 offset, bool attach,
                                                  float startDelay, float fadeIn, float hold, float fadeOut)
    {
        if (!prefab || !combat) yield break;
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        var go = Instantiate(prefab, combat.transform.position, Quaternion.identity);
        go.transform.localScale = go.transform.localScale * Mathf.Max(0.0001f, vfxScaleMul);
        if (attach) go.transform.SetParent(combat.transform, true);

        var follower = go.GetComponent<VFXFollowFlip>() ?? go.AddComponent<VFXFollowFlip>();
        follower.Init(combat.transform, PlayerSR, offset, attach);

        var sr = go.GetComponentInChildren<SpriteRenderer>();
        var cg = go.GetComponentInChildren<CanvasGroup>();

        if (sr != null)
        {
            Color c = sr.color;
            if (fadeIn > 0f) { float t = 0f; while (t < fadeIn) { sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0f, 1f, t / fadeIn)); t += Time.deltaTime; yield return null; } sr.color = new Color(c.r, c.g, c.b, 1f); }
            if (hold > 0f) yield return new WaitForSeconds(hold);
            if (fadeOut > 0f) { float t = 0f; while (t < fadeOut) { sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, t / fadeOut)); t += Time.deltaTime; yield return null; } }
            Destroy(go); yield break;
        }
        if (cg != null)
        {
            if (fadeIn > 0f) { float t = 0f; while (t < fadeIn) { cg.alpha = Mathf.Lerp(0f, 1f, t / fadeIn); t += Time.deltaTime; yield return null; } cg.alpha = 1f; }
            if (hold > 0f) yield return new WaitForSeconds(hold);
            if (fadeOut > 0f) { float t = 0f; while (t < fadeOut) { cg.alpha = Mathf.Lerp(1f, 0f, t / fadeOut); t += Time.deltaTime; yield return null; } }
            Destroy(go); yield break;
        }

        // 렌더러 없으면 수명만 기다렸다 제거(페이드 불가)
        if (fadeIn > 0f) yield return new WaitForSeconds(fadeIn);
        if (hold > 0f) yield return new WaitForSeconds(hold);
        if (fadeOut > 0f) yield return new WaitForSeconds(fadeOut);
        Destroy(go);
    }

    private void DoHitbox(float dmg, float knock, float range, float radius)
    {
        if (!combat) return;
        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 center = (Vector2)combat.transform.position + facing.normalized * range;

        var hits = Physics2D.OverlapCircleAll(center, radius, combat.EnemyMask);
        if (hits == null || hits.Length == 0) return;

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
                dmgTarget.ApplyHit(dmg, knock, dir, combat.gameObject);
            }
        }
    }
}
