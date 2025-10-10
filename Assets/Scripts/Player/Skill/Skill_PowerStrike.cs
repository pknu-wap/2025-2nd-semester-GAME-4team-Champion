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
    [SerializeField] private string triggerName = "Skill_PowerStrike";
    [SerializeField] private bool lockMoveDuringSkill = true;

    [Header("Cooldown")]
    [SerializeField] private float cooldownSeconds = 8f;
    private float lastCastEndTime = -999f;
    public float CooldownRemain => Mathf.Max(0f, (lastCastEndTime + cooldownSeconds) - Time.time);
    public bool IsOnCooldown => CooldownRemain > 0f;

    [Header("VFX (optional)")]
    [SerializeField] private GameObject vfxPrefab;
    [SerializeField] private Vector2 vfxOffset = new Vector2(0.6f, 0f);
    [SerializeField] private bool attachToPlayer = true;
    [SerializeField] private bool flipOnLeft = true;

    // ==== TAG ====
    public const string TAG_POWERSTRIKE_CAST = "Tag.Skill.PowerStrike.Cast";
    public event System.Action<string> OnTag;

    // reuse
    private static readonly HashSet<int> _seenIds = new HashSet<int>(32);
    private Coroutine castCo;

    private void Reset()
    {
        if (!attack) attack = GetComponentInParent<PlayerAttack>();
        if (!combat) combat = GetComponentInParent<PlayerCombat>();
        if (!moveRef) moveRef = GetComponentInParent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponentInParent<Animator>();
    }

    public string SkillName => "PowerStrike";

    public bool TryCastSkill(PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        combat = c; moveRef = m; animator = a;
        return TryCastInternal();
    }

    public bool TryCastSkill(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        attack = owner; combat = c; moveRef = m; animator = a;
        return TryCastInternal();
    }

    private bool TryCastInternal()
    {
        if (!combat || !moveRef || !animator) return false;
        if (IsOnCooldown) return false;

        if (combat.HP <= 0f) return false;
        var reviver = combat.GetComponent<Player_Revive>(); if (reviver != null && reviver.IsReviving) return false;
        var healer = combat.GetComponent<Player_Heal>(); if (healer != null && healer.IsHealing) return false;
        if (combat.IsActionLocked || combat.IsStaminaBroken) return false;

        if (castCo != null) StopCoroutine(castCo);
        castCo = StartCoroutine(CastRoutine());
        return true;
    }

    private IEnumerator CastRoutine()
    {
        float total = windup + active + recovery;

        if (lockMoveDuringSkill)
            combat.StartActionLock(total, true);

        var vfxCo = StartCoroutine(SpawnAndFadeVFX());

        // 선딜
        yield return new WaitForSeconds(windup);

        // 애니메이션
        if (!string.IsNullOrEmpty(triggerName))
            animator.SetTrigger(triggerName);

        // 🔸 태그: 공격 시전 시점
        OnTag?.Invoke(TAG_POWERSTRIKE_CAST);

        // 히트 계산
        var stats = (attack != null && attack.baseStats != null)
            ? attack.baseStats
            : new PlayerAttack.AttackBaseStats();

        float finalDamage = stats.baseDamage * damageMul;
        float finalKnock = stats.baseKnockback * knockMul;
        float finalRange = stats.baseRange * rangeMul;
        float finalRadius = stats.baseRadius * radiusMul;

        DoHitbox(finalDamage, finalKnock, finalRange, finalRadius);

        yield return new WaitForSeconds(active + recovery);

        combat.EnterCombat("Skill_PowerStrike");
        lastCastEndTime = Time.time;
        castCo = null;
    }

    private IEnumerator SpawnAndFadeVFX()
    {
        if (!vfxPrefab || !combat) yield break;

        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector3 basePos = combat.transform.position;
        Vector3 spawnPos = basePos + (Vector3)(facing.normalized * vfxOffset.x + new Vector2(0f, vfxOffset.y));

        var go = Instantiate(vfxPrefab, spawnPos, Quaternion.identity);
        if (attachToPlayer) go.transform.SetParent(combat.transform);

        if (flipOnLeft && facing.x < 0f)
        {
            var s = go.transform.localScale;
            s.x = Mathf.Abs(s.x) * -1f;
            go.transform.localScale = s;
        }

        var sr = go.GetComponentInChildren<SpriteRenderer>();
        var cg = go.GetComponentInChildren<CanvasGroup>();
        var ps = go.GetComponentInChildren<ParticleSystem>();

        if (sr != null)
        {
            Color c = sr.color;
            float t = 0f;
            while (t < 0.05f) { sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0f, 1f, t / 0.05f)); t += Time.deltaTime; yield return null; }
            sr.color = new Color(c.r, c.g, c.b, 1f);
            t = 0f;
            while (t < 0.5f) { sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, t / 0.5f)); t += Time.deltaTime; yield return null; }
            Destroy(go); yield break;
        }
        if (cg != null)
        {
            float t = 0f;
            while (t < 0.05f) { cg.alpha = Mathf.Lerp(0f, 1f, t / 0.05f); t += Time.deltaTime; yield return null; }
            cg.alpha = 1f; t = 0f;
            while (t < 0.5f) { cg.alpha = Mathf.Lerp(1f, 0f, t / 0.5f); t += Time.deltaTime; yield return null; }
            Destroy(go); yield break;
        }
        if (ps != null) { ps.Play(); yield return new WaitForSeconds(0.35f); Destroy(go); yield break; }
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
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (!h) continue;

            int rid = h.transform.root.GetInstanceID();
            if (!_seenIds.Add(rid)) continue;

            Vector2 toEnemy = ((Vector2)h.transform.position - (Vector2)combat.transform.position).normalized;
            var dmgTarget = h.GetComponentInParent<IDamageable>();
            if (dmgTarget != null)
                dmgTarget.ApplyHit(dmg, knock, toEnemy, combat.gameObject);
        }
    }
}
