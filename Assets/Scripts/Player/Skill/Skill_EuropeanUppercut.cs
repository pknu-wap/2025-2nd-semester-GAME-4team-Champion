using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skill_EuropeanUppercut : MonoBehaviour, IPlayerSkill, IChargeSkill
{
    [Header("Refs")]
    [SerializeField] private PlayerAttack attack;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;
    [SerializeField] private CameraLockOn cameraLockOn;

    [Header("Hitbox / Base")]
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
    [SerializeField] private string chargeBool = "E_Charging";
    [SerializeField] private string fireTrigger = "E_Uppercut";

    // ==== VFX ====
    [Header("VFX — Charge Loop")]
    [SerializeField] private GameObject chargeVFX;
    [SerializeField] private bool chargeAttachToPlayer = true;
    [SerializeField] private Vector2 chargeVfxOffset = new Vector2(0.2f, 0f);
    [SerializeField] private float chargeVfxFadeIn = 0.05f;
    [SerializeField] private float chargeVfxFadeOut = 0.25f;

    [Header("VFX — Fire")]
    [SerializeField] private GameObject fireVFX;
    [SerializeField] private bool fireAttachToPlayer = false;
    [SerializeField] private Vector2 fireVfxOffset = new Vector2(0.6f, 0f);
    [SerializeField] private float fireVfxStartDelay = 0f; // 🔸 시작 타이밍
    [SerializeField] private float fireVfxFadeIn = 0.03f;
    [SerializeField] private float fireVfxHold = 0f;
    [SerializeField] private float fireVfxFadeOut = 0.35f;

    // ==== TAG ====
    public const string TAG_UPPERCUT_MIN = "Tag.Skill.Uppercut.Min";
    public const string TAG_UPPERCUT_MAX = "Tag.Skill.Uppercut.Max";
    public event System.Action<string> OnTag;

    private static readonly HashSet<int> _seenIds = new HashSet<int>(32);

    public string SkillName => "European Uppercut";
    public float GetTotalDuration() => windup + active + recovery;

    public bool IsCharging { get; private set; }
    public bool IsAttacking { get; private set; }
    public float CooldownRemain => Mathf.Max(0f, (lastCastEndTime + cooldownSeconds) - Time.time);
    public bool IsOnCooldown => CooldownRemain > 0f;

    private float chargeStartTime;
    private float lastCastEndTime = -999f;
    private Coroutine waitFullChargeCo;
    private GameObject chargeFxInstance;
    private SpriteRenderer _cachedPlayerSR;
    private SpriteRenderer PlayerSR => _cachedPlayerSR ??= combat.GetComponentInChildren<SpriteRenderer>();

    public void Bind(PlayerAttack atk, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    { attack = atk; combat = c; moveRef = m; animator = a; }

    public bool TryStartCharge(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        if (owner) attack = owner; if (c) combat = c; if (m) moveRef = m; if (a) animator = a;
        if (!cameraLockOn)
            cameraLockOn = FindFirstObjectByType<CameraLockOn>(FindObjectsInactive.Include);
        return OnChargeStarted();

    }

    public void ReleaseCharge() => OnChargeReleased();
    public bool TryCastSkill(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a) => TryStartCharge(owner, c, m, a);

    public bool OnChargeStarted()
    {
        if (!attack || !combat || !moveRef || !animator) return false;
        if (IsAttacking || IsCharging) return false;
        if (IsOnCooldown) return false;
        if (combat.HP <= 0f || combat.IsActionLocked || combat.IsStaminaBroken) return false;

        attack.FreezeComboTimerFor(fullChargeTime + GetTotalDuration() + 0.2f);
        TagBus.Raise("Tag.Zoom");
        cameraLockOn.SuppressLockOnZoom(3f);
        IsCharging = true;
        chargeStartTime = Time.time;

        if (lockMoveDuringCharge) moveRef.SetMovementLocked(true, false, true);
        animator.SetBool(chargeBool, true);

        SpawnChargeVFX(); // 🔸 페이드 인 포함

        if (waitFullChargeCo != null) StopCoroutine(waitFullChargeCo);
        waitFullChargeCo = StartCoroutine(WaitAndAutoFire());
        return true;
    }

    public void OnPlayerHitInterrupt(PlayerHit.HitInterruptInfo info)
    {
        if (IsCharging)
        {
            // 차지만 취소(발동은 유지)
            IsCharging = false;
            animator?.SetBool(chargeBool, false);
            if (lockMoveDuringCharge) moveRef?.SetMovementLocked(false, false);
            if (waitFullChargeCo != null) { StopCoroutine(waitFullChargeCo); waitFullChargeCo = null; }
            // KillChargeVFX();
        }
        // IsAttacking(발동 중)일 때는 취소하지 않음(요청사항)
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
            if ((Time.time - chargeStartTime) >= fullChargeTime)
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

        // Charge VFX 페이드 아웃
        yield return StartCoroutine(FadeOutAndKillChargeVFX());

        var stats = (attack && attack.baseStats != null) ? attack.baseStats : new PlayerAttack.AttackBaseStats();
        float dmgMul = ComputeDamageMul(heldSec);
        float dmg = stats.baseDamage * dmgMul;
        float range = stats.baseRange * rangeMul;
        float radius = stats.baseRadius * radiusMul;

        if (heldSec <= fixed2xWindow + 0.0001f) OnTag?.Invoke(TAG_UPPERCUT_MIN);
        else if (heldSec >= fullChargeTime - 0.0001f) OnTag?.Invoke(TAG_UPPERCUT_MAX);

        float total = GetTotalDuration();
        combat.StartActionLock(total, true);
        IsAttacking = true;

        if (!string.IsNullOrEmpty(fireTrigger)) animator.SetTrigger(fireTrigger);
        yield return new WaitForSeconds(windup);

        // Fire VFX (딜레이+페이드)
        StartCoroutine(SpawnFireVFXWithFade());

        DoHitbox(dmg, range, radius);
        TagBus.Raise("Tag.impact(L)");
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

    private void DoHitbox(float dmg, float range, float radius)
    {
        if (!combat) return;
        Vector2 facing = (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
        Vector2 center = (Vector2)combat.transform.position + facing.normalized * range;

        var hits = Physics2D.OverlapCircleAll(center, radius, enemyMaskOverride.value != 0 ? enemyMaskOverride : combat.EnemyMask);
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
                dmgTarget.ApplyHit(dmg, dir, combat.gameObject);
            }
        }
    }

    // --- VFX Helpers ---
    private void SpawnChargeVFX()
    {
        if (!chargeVFX || !combat || chargeFxInstance) return;

        chargeFxInstance = Instantiate(chargeVFX, combat.transform.position, Quaternion.identity);
        var follower = chargeFxInstance.GetComponent<VFXFollowFlip>() ?? chargeFxInstance.AddComponent<VFXFollowFlip>();
        follower.Init(combat.transform, PlayerSR, chargeVfxOffset, chargeAttachToPlayer);

        var sr = chargeFxInstance.GetComponentInChildren<SpriteRenderer>();
        var cg = chargeFxInstance.GetComponentInChildren<CanvasGroup>();

        if (sr != null) { Color c = sr.color; sr.color = new Color(c.r, c.g, c.b, 0f); StartCoroutine(FadeSprite(sr, 0f, 1f, chargeVfxFadeIn)); }
        else if (cg != null) { cg.alpha = 0f; StartCoroutine(FadeCanvas(cg, 0f, 1f, chargeVfxFadeIn)); }
    }

    private IEnumerator FadeOutAndKillChargeVFX()
    {
        if (!chargeFxInstance) yield break;

        var sr = chargeFxInstance.GetComponentInChildren<SpriteRenderer>();
        var cg = chargeFxInstance.GetComponentInChildren<CanvasGroup>();

        if (sr != null) yield return FadeSprite(sr, sr.color.a, 0f, chargeVfxFadeOut);
        else if (cg != null) yield return FadeCanvas(cg, cg.alpha, 0f, chargeVfxFadeOut);

        Destroy(chargeFxInstance);
        chargeFxInstance = null;
    }

    private IEnumerator SpawnFireVFXWithFade()
    {
        if (!fireVFX || !combat) yield break;
        if (fireVfxStartDelay > 0f) yield return new WaitForSeconds(fireVfxStartDelay);

        var go = Instantiate(fireVFX, combat.transform.position, Quaternion.identity);
        var follower = go.GetComponent<VFXFollowFlip>() ?? go.AddComponent<VFXFollowFlip>();
        follower.Init(combat.transform, PlayerSR, fireVfxOffset, fireAttachToPlayer);

        var sr = go.GetComponentInChildren<SpriteRenderer>();
        var cg = go.GetComponentInChildren<CanvasGroup>();

        if (sr != null)
        {
            Color c = sr.color; sr.color = new Color(c.r, c.g, c.b, 0f);
            if (fireVfxFadeIn > 0f) yield return FadeSprite(sr, 0f, 1f, fireVfxFadeIn);
            if (fireVfxHold > 0f) yield return new WaitForSeconds(fireVfxHold);
            if (fireVfxFadeOut > 0f) yield return FadeSprite(sr, 1f, 0f, fireVfxFadeOut);
            Destroy(go); yield break;
        }
        if (cg != null)
        {
            cg.alpha = 0f;
            if (fireVfxFadeIn > 0f) yield return FadeCanvas(cg, 0f, 1f, fireVfxFadeIn);
            if (fireVfxHold > 0f) yield return new WaitForSeconds(fireVfxHold);
            if (fireVfxFadeOut > 0f) yield return FadeCanvas(cg, 1f, 0f, fireVfxFadeOut);
            Destroy(go); yield break;
        }

        float t = fireVfxFadeIn + fireVfxHold + fireVfxFadeOut;
        if (t > 0f) yield return new WaitForSeconds(t);
        Destroy(go);
    }

    private IEnumerator FadeSprite(SpriteRenderer sr, float from, float to, float time)
    {
        if (time <= 0f) { var c = sr.color; sr.color = new Color(c.r, c.g, c.b, to); yield break; }
        float t = 0f; var col = sr.color;
        while (t < time) { sr.color = new Color(col.r, col.g, col.b, Mathf.Lerp(from, to, t / time)); t += Time.deltaTime; yield return null; }
        sr.color = new Color(col.r, col.g, col.b, to);
    }
    private IEnumerator FadeCanvas(CanvasGroup cg, float from, float to, float time)
    {
        if (time <= 0f) { cg.alpha = to; yield break; }
        float t = 0f;
        while (t < time) { cg.alpha = Mathf.Lerp(from, to, t / time); t += Time.deltaTime; yield return null; }
        cg.alpha = to;
    }
}
