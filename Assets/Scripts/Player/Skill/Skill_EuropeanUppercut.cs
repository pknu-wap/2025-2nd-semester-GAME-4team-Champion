using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// European Uppercut - 차지형 스킬
/// - 키 해제 시: 0~0.5s → x2 고정(최소)
/// - 0.5~3.0s: x2→x5 선형
/// - 3.0s: 자동 발동(최대)
/// </summary>
public class Skill_EuropeanUppercut : MonoBehaviour, IPlayerSkill, IChargeSkill
{
    [Header("Refs")]
    [SerializeField] private PlayerAttack attack;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;

    [Header("Hitbox / Base from PlayerAttack")]
    [SerializeField] private LayerMask enemyMaskOverride;
    [SerializeField] private float knockMul = 1.2f;
    [SerializeField] private float rangeMul = 1.0f;
    [SerializeField] private float radiusMul = 1.0f;

    [Header("Timing (sec)")]
    [SerializeField] private float windup = 0.08f;
    [SerializeField] private float active = 0.06f;
    [SerializeField] private float recovery = 0.24f;

    [Header("Charge (sec)")]
    [SerializeField] private float fixed2xWindow = 0.5f;
    [SerializeField] private float fullChargeTime = 3.0f;
    [SerializeField] private bool lockMoveDuringCharge = true;

    [Header("Cooldown")]
    [SerializeField] private float cooldownSeconds = 8f;

    [Header("Animation")]
    [SerializeField] private string chargeBool = "U_Charging";
    [SerializeField] private string fireTrigger = "Uppercut";

    [Header("VFX (optional)")]
    [SerializeField] private GameObject vfxPrefab;
    [SerializeField] private Transform vfxPoint;
    [SerializeField] private float vfxLifetime = 0.6f;

    // ==== TAG ====
    public const string TAG_UPPERCUT_MIN = "Tag.Skill.Uppercut.Min"; // x2
    public const string TAG_UPPERCUT_MAX = "Tag.Skill.Uppercut.Max"; // x5
    public event System.Action<string> OnTag;

    // reuse
    private static readonly HashSet<int> _seenIds = new HashSet<int>(32);

    // ===== State =====
    public string SkillName => "European Uppercut";
    public float GetTotalDuration() => windup + active + recovery;

    public bool IsCharging { get; private set; }
    public bool IsAttacking { get; private set; }
    public float CooldownRemain => Mathf.Max(0f, (lastCastEndTime + cooldownSeconds) - Time.time);
    public bool IsOnCooldown => CooldownRemain > 0f;

    private float chargeStartTime;
    private float lastCastEndTime = -999f;
    private Coroutine waitFullChargeCo;

    public void Bind(PlayerAttack atk, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    { attack = atk; combat = c; moveRef = m; animator = a; }

    public bool TryStartCharge(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    { if (owner) attack = owner; if (c) combat = c; if (m) moveRef = m; if (a) animator = a; return OnChargeStarted(); }

    public void ReleaseCharge() => OnChargeReleased();

    public bool TryCastSkill(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    { return TryStartCharge(owner, c, m, a); }

    public bool OnChargeStarted()
    {
        if (!attack || !combat || !moveRef || !animator) return false;
        if (IsAttacking || IsCharging) return false;
        if (IsOnCooldown) return false;
        if (combat.HP <= 0f || combat.IsActionLocked || combat.IsStaminaBroken) return false;

        attack.FreezeComboTimerFor(fullChargeTime + GetTotalDuration() + 0.2f);

        IsCharging = true;
        chargeStartTime = Time.time;

        if (lockMoveDuringCharge) moveRef.SetMovementLocked(true, false, true);
        animator.SetBool(chargeBool, true);

        if (waitFullChargeCo != null) StopCoroutine(waitFullChargeCo);
        waitFullChargeCo = StartCoroutine(WaitAndAutoFire());

        return true;
    }

    public void OnChargeReleased()
    {
        if (!IsCharging) return;

        float held = Time.time - chargeStartTime;
        IsCharging = false;
        if (waitFullChargeCo != null) { StopCoroutine(waitFullChargeCo); waitFullChargeCo = null; }
        StartCoroutine(FireWithHeld(held));
    }

    private IEnumerator WaitAndAutoFire()
    {
        while (IsCharging)
        {
            float held = Time.time - chargeStartTime;
            if (held >= fullChargeTime)
            {
                IsCharging = false;
                yield return StartCoroutine(FireWithHeld(fullChargeTime));
                break;
            }
            yield return null;
        }
        waitFullChargeCo = null;
    }

    private IEnumerator FireWithHeld(float heldSec)
    {
        animator.SetBool(chargeBool, false);
        if (lockMoveDuringCharge) moveRef.SetMovementLocked(false, false);

        var stats = (attack && attack.baseStats != null) ? attack.baseStats : new PlayerAttack.AttackBaseStats();
        float dmgMul = ComputeDamageMul(heldSec);   // 0~0.5: 2, 3.0: 5
        float dmg = stats.baseDamage * dmgMul;
        float knock = stats.baseKnockback * knockMul;
        float range = stats.baseRange * rangeMul;
        float radius = stats.baseRadius * radiusMul;

        // 태그: 최소/최대 데미지 구간
        if (heldSec <= fixed2xWindow + 0.0001f) OnTag?.Invoke(TAG_UPPERCUT_MIN);
        else if (heldSec >= fullChargeTime - 0.0001f) OnTag?.Invoke(TAG_UPPERCUT_MAX);

        float total = GetTotalDuration();
        combat.StartActionLock(total, true);
        IsAttacking = true;

        if (!string.IsNullOrEmpty(fireTrigger)) animator.SetTrigger(fireTrigger);
        yield return new WaitForSeconds(windup);

        DoHitbox(dmg, knock, range, radius);

        if (vfxPrefab)
        {
            Transform p = vfxPoint ? vfxPoint : transform;
            var go = Instantiate(vfxPrefab, p.position, p.rotation);
            Destroy(go, vfxLifetime);
        }

        yield return new WaitForSeconds(active + recovery);

        combat.EnterCombat("Skill_EuropeanUppercut");
        lastCastEndTime = Time.time;
        IsAttacking = false;
    }

    private float ComputeDamageMul(float held)
    {
        if (held <= fixed2xWindow) return 2f;
        if (held >= fullChargeTime) return 5f;
        float t = Mathf.InverseLerp(fixed2xWindow, fullChargeTime, held);
        return Mathf.Lerp(2f, 5f, t);
    }

    private void DoHitbox(float dmg, float knock, float range, float radius)
    {
        if (!combat) return;

        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 center = (Vector2)combat.transform.position + facing.normalized * range;

        LayerMask mask = enemyMaskOverride.value != 0 ? enemyMaskOverride : combat.EnemyMask;
        var hits = Physics2D.OverlapCircleAll(center, radius, mask);
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
