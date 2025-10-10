using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rapid Fire - 연타형 스킬
/// - 시작: 1.0s 자동(5타/초)
/// - 이후: 연타 유지 시 최대 3.0s
/// - 풀(3.0s) 유지 시 마지막 ×2 피니시
/// </summary>
public class Skill_RapidFire : MonoBehaviour, IPlayerSkill
{
    [Header("Refs")]
    [SerializeField] private PlayerAttack attack;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    [Header("Damage & Hitbox")]
    [SerializeField] private float rapidDamageMul = 0.3f;
    [SerializeField] private float finisherDamageMul = 2.0f;
    [SerializeField] private float knockMul = 1.0f;
    [SerializeField] private float rangeMul = 1.0f;
    [SerializeField] private float radiusMul = 1.0f;

    [Header("Timing")]
    [SerializeField] private float baseDuration = 1.0f;
    [SerializeField] private float maxDuration = 3.0f;
    [SerializeField] private float hitsPerSecond = 5f;
    [SerializeField] private float stopGraceAfterBase = 0.35f;

    [Header("Cooldown")]
    [SerializeField] private float cooldownSeconds = 6f;
    private float lastCastEndTime = -999f;

    [Header("Animation (optional)")]
    [SerializeField] private string animStartTrigger = "RapidFire_Start";
    [SerializeField] private string animLoopBool = "RapidFire_Loop";
    [SerializeField] private string animEndTrigger = "RapidFire_End";
    [SerializeField] private string animFinishTrigger = "RapidFire_Finish";

    // ==== TAG ====
    public const string TAG_RAPID_FINISHER = "Tag.Skill.RapidFire.Finisher"; // 3초 피니시 x2
    public event System.Action<string> OnTag;

    // reuse
    private static readonly HashSet<int> _seenIds = new HashSet<int>(32);

    // ==== State ====
    private bool isActive;
    private float startedAt;
    private float lastPressAt;
    private float nextHitAt;
    private Coroutine loopCo;

    public string SkillName => "Rapid Fire";
    public float GetTotalDuration() => baseDuration;
    public float CooldownRemain => Mathf.Max(0f, (lastCastEndTime + cooldownSeconds) - Time.time);
    public bool IsOnCooldown => CooldownRemain > 0f;

    public bool TryCastSkill(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        if (owner) attack = owner;
        if (c) combat = c;
        if (m) moveRef = m;
        if (a) animator = a;

        if (!attack || !combat || !moveRef || !animator) return false;

        if (isActive)
        {
            lastPressAt = Time.time;
            attack.FreezeComboTimerFor(stopGraceAfterBase + 0.1f);
            return false;
        }

        if (IsOnCooldown) return false;
        if (combat.HP <= 0f || combat.IsActionLocked || combat.IsStaminaBroken) return false;

        startedAt = lastPressAt = Time.time;
        nextHitAt = startedAt;
        isActive = true;

        attack.FreezeComboTimerFor(baseDuration + 0.1f);

        if (!string.IsNullOrEmpty(animStartTrigger)) animator.SetTrigger(animStartTrigger);
        if (!string.IsNullOrEmpty(animLoopBool)) animator.SetBool(animLoopBool, true);

        if (loopCo != null) StopCoroutine(loopCo);
        loopCo = StartCoroutine(Loop());

        return true;
    }

    private IEnumerator Loop()
    {
        float interval = 1f / Mathf.Max(0.0001f, hitsPerSecond);
        bool finishedFull = false;

        while (isActive)
        {
            float now = Time.time;
            float elapsed = now - startedAt;

            if (now >= nextHitAt)
            {
                DoRapidHit();
                nextHitAt += interval;
            }

            if (elapsed < baseDuration)
            {
                // 기본 1초 자동 유지
            }
            else
            {
                bool stoppedMashing = (now - lastPressAt) > stopGraceAfterBase;

                if (elapsed >= maxDuration)
                {
                    finishedFull = true;
                    break;
                }

                if (stoppedMashing)
                {
                    finishedFull = false;
                    break;
                }

                attack.FreezeComboTimerFor(0.15f);
            }

            yield return null;
        }

        EndRapidFire(finishedFull);
    }

    private void EndRapidFire(bool fullFinish)
    {
        if (!isActive) return;
        isActive = false;

        if (!string.IsNullOrEmpty(animLoopBool)) animator.SetBool(animLoopBool, false);
        if (fullFinish)
        {
            if (!string.IsNullOrEmpty(animFinishTrigger)) animator.SetTrigger(animFinishTrigger);
            DoFinisherHit(); // x2 데미지 일격
        }
        else
        {
            if (!string.IsNullOrEmpty(animEndTrigger)) animator.SetTrigger(animEndTrigger);
        }

        lastCastEndTime = Time.time;
    }

    private void DoRapidHit()
    {
        var stats = (attack && attack.baseStats != null) ? attack.baseStats : new PlayerAttack.AttackBaseStats();
        float dmg = stats.baseDamage * rapidDamageMul;
        float knock = stats.baseKnockback * knockMul;
        float range = stats.baseRange * rangeMul;
        float radius = stats.baseRadius * radiusMul;

        Hitbox(dmg, knock, range, radius);
    }

    private void DoFinisherHit()
    {
        var stats = (attack && attack.baseStats != null) ? attack.baseStats : new PlayerAttack.AttackBaseStats();
        float dmg = stats.baseDamage * finisherDamageMul; // x2
        float knock = stats.baseKnockback * knockMul;
        float range = stats.baseRange * rangeMul;
        float radius = stats.baseRadius * radiusMul;

        Hitbox(dmg, knock, range, radius);

        // 🔸 태그: 3초 피니시 일격
        OnTag?.Invoke(TAG_RAPID_FINISHER);

        combat.EnterCombat("RapidFire_Finish");
    }

    private void Hitbox(float dmg, float knock, float range, float radius)
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

            Vector2 dir = ((Vector2)h.transform.position - (Vector2)combat.transform.position).normalized;
            var dmgTarget = h.GetComponentInParent<IDamageable>();
            if (dmgTarget != null) dmgTarget.ApplyHit(dmg, knock, dir, combat.gameObject);
        }
    }

    private void OnDisable()
    {
        if (loopCo != null) StopCoroutine(loopCo);
        loopCo = null;
        isActive = false;
        if (!string.IsNullOrEmpty(animLoopBool) && animator) animator.SetBool(animLoopBool, false);
    }
}
