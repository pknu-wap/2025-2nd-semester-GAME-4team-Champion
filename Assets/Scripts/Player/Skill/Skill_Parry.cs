using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skill_Parry : MonoBehaviour, IPlayerSkill, IParryWindowProvider
{
    [Header("Refs")]
    [SerializeField] private PlayerAttack attack;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    [Header("Timings (sec)")]
    [SerializeField] private float windup = 0.10f;
    [SerializeField] private float parryWindow = 0.20f;
    [SerializeField] private float recovery = 0.50f;

    [Header("On Success (Counter xN)")]
    [SerializeField] private float damageMulOnParry = 2.5f;
    [SerializeField] private float knockMul = 1.0f;
    [SerializeField] private float rangeMul = 1.0f;
    [SerializeField] private float radiusMul = 1.0f;
    [SerializeField] private float betweenHits = 0.10f;

    [Header("Cooldown")]
    [SerializeField] private float cooldownSeconds = 5f;
    private float lastCastEndTime = -999f;
    private float lastAppliedCooldown = 0f;

    [Header("Animation (optional)")]
    [SerializeField] private string animParryStartTrigger = "ParryStart";
    [SerializeField] private string animParrySuccessTrigger = "ParrySuccess";
    [SerializeField] private string animParryFailTrigger = "ParryFail";

    // ==== TAG ====
    public const string TAG_PARRY_SUCCESS = "Tag.Skill.Parry.Success";
    public event System.Action<string> OnTag;

    // reuse
    private static readonly HashSet<int> _seenIds = new HashSet<int>(32);

    // ==== State ====
    private bool isCasting;
    private bool successParry;

    private bool windowActive;
    private float windowEndTime;

    public string SkillName => "Parry";
    public float GetTotalDuration() => windup + parryWindow + recovery;

    public bool TryCastSkill(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        if (owner) attack = owner;
        if (c) combat = c;
        if (m) moveRef = m;
        if (a) animator = a;

        if (isCasting) return false;
        if (IsOnCooldown) return false;
        if (!attack || !combat || !moveRef || !animator) return false;
        if (combat.HP <= 0f || combat.IsActionLocked || combat.IsStaminaBroken) return false;

        StartCoroutine(CastRoutine());
        return true;
    }

    // IParryWindowProvider
    public bool IsParryWindowActive => windowActive && Time.time <= windowEndTime;

    public void OnParrySuccess()
    {
        if (!isCasting || successParry) return;
        successParry = true;

        // 🔸 태그: 패링 성공
        OnTag?.Invoke(TAG_PARRY_SUCCESS);

        StartCoroutine(DoParryCounters());
    }

    // 쿨다운 관련
    public float CooldownRemain => Mathf.Max(0f, (lastCastEndTime + lastAppliedCooldown) - Time.time);
    public bool IsOnCooldown => CooldownRemain > 0f;

    private IEnumerator CastRoutine()
    {
        isCasting = true;
        successParry = false;
        windowActive = false;

        attack.FreezeComboTimerFor(GetTotalDuration() + 0.1f);

        try
        {
            if (!string.IsNullOrEmpty(animParryStartTrigger))
                animator.SetTrigger(animParryStartTrigger);
            yield return new WaitForSeconds(windup);

            windowActive = true;
            windowEndTime = Time.time + parryWindow;

            while (Time.time <= windowEndTime)
                yield return null;

            windowActive = false;

            if (successParry)
            {
                if (!string.IsNullOrEmpty(animParrySuccessTrigger))
                    animator.SetTrigger(animParrySuccessTrigger);
            }
            else
            {
                if (!string.IsNullOrEmpty(animParryFailTrigger))
                    animator.SetTrigger(animParryFailTrigger);
            }
            yield return new WaitForSeconds(recovery);
        }
        finally
        {
            lastCastEndTime = Time.time;
            lastAppliedCooldown = successParry ? cooldownSeconds * 0.5f : cooldownSeconds;

            windowActive = false;
            isCasting = false;
        }
    }

    private IEnumerator DoParryCounters()
    {
        var stats = (attack && attack.baseStats != null) ? attack.baseStats : new PlayerAttack.AttackBaseStats();
        float dmg = stats.baseDamage * damageMulOnParry;
        float knock = stats.baseKnockback * knockMul;
        float range = stats.baseRange * rangeMul;
        float radius = stats.baseRadius * radiusMul;

        HitOnce(dmg, knock, range, radius);
        yield return new WaitForSeconds(betweenHits);
        HitOnce(dmg, knock, range, radius);

        combat.EnterCombat("Parry_Counter");
    }

    private void HitOnce(float dmg, float knock, float range, float radius)
    {
        if (!combat) return;

        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 center = (Vector2)combat.transform.position + facing.normalized * range;
        var hits = Physics2D.OverlapCircleAll(center, radius, combat.EnemyMask);
        if (hits == null || hits.Length == 0) return;

        _seenIds.Clear();
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i]; if (!h) continue;
            int rid = h.transform.root.GetInstanceID();
            if (!_seenIds.Add(rid)) continue;

            Vector2 dir = ((Vector2)h.transform.position - (Vector2)combat.transform.position).normalized;
            var dmgTarget = h.GetComponentInParent<IDamageable>();
            if (dmgTarget != null) dmgTarget.ApplyHit(dmg, knock, dir, combat.gameObject);
        }
    }
}
