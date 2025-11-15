using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skill_RapidFire : MonoBehaviour, IPlayerSkill
{
    [Header("Refs")]
    [SerializeField] private PlayerAttack attack;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerMoveBehaviour moveRef;
    [SerializeField] private Animator animator;
    [SerializeField] private CameraLockOn cameraLockOn;

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

    [Header("Movement Lock")]
    [SerializeField] private bool lockMoveDuringSkill = true;
    private const string LOCK_RF = "LOCK_RAPIDFIRE";

    [Header("Finisher Timing (sec)")]
    [SerializeField] private float finisherWindup = 0.20f; // 선딜
    [SerializeField] private float finisherRecovery = 0.30f; // 후딜

    [Header("Cooldown")]
    [SerializeField] private float cooldownSeconds = 6f;
    private float lastCastEndTime = -999f;

    [Header("Animation")]
    [SerializeField] private string animStartTrigger = "RapidFire_Start";
    [SerializeField] private string animLoopBool = "RapidFire_Loop";
    [SerializeField] private string animEndTrigger = "RapidFire_End";
    [SerializeField] private string animFinishTrigger = "RapidFire_Finish";

    [Header("VFX — Rapid (순환 5개)")]
    [SerializeField] private GameObject[] rapidVFX = new GameObject[5];
    [SerializeField] private Vector2 rapidVfxOffset = new Vector2(0.5f, 0f);
    [SerializeField] private bool rapidAttachToPlayer = true;
    [SerializeField] private float rapidVfxStartDelay = 0f;
    [SerializeField] private float rapidVfxFadeIn = 0.02f;
    [SerializeField] private float rapidVfxHold = 0f;
    [SerializeField] private float rapidVfxFadeOut = 0.18f;

    [Header("VFX — Finisher")]
    [SerializeField] private GameObject finisherVFX;
    [SerializeField] private Vector2 finisherVfxOffset = new Vector2(0.7f, 0f);
    [SerializeField] private bool finisherAttachToPlayer = true;
    [SerializeField] private float finisherVfxStartDelay = 0f;
    [SerializeField] private float finisherVfxFadeIn = 0.02f;
    [SerializeField] private float finisherVfxHold = 0.05f;
    [SerializeField] private float finisherVfxFadeOut = 0.25f;

    public const string TAG_RAPID_FINISHER = "Tag.Skill.RapidFire.Finisher";
    public event System.Action<string> OnTag;

    private static readonly HashSet<int> _seenIds = new HashSet<int>(32);
    private SpriteRenderer _cachedPlayerSR;
    private SpriteRenderer PlayerSR => _cachedPlayerSR ??= combat.GetComponentInChildren<SpriteRenderer>();

    private bool isActive;
    private float startedAt;
    private float lastPressAt;
    private float nextHitAt;
    private Coroutine loopCo;
    private int rapidVfxIndex = 0;

    public string SkillName => "Rapid Fire";
    public float GetTotalDuration() => baseDuration;
    public float CooldownRemain => Mathf.Max(0f, (lastCastEndTime + cooldownSeconds) - Time.time);
    public bool IsOnCooldown => CooldownRemain > 0f;

    public bool TryCastSkill(PlayerAttack owner, PlayerCombat c, PlayerMoveBehaviour m, Animator a)
    {
        if (owner) attack = owner; if (c) combat = c; if (m) moveRef = m; if (a) animator = a;
        if (!attack || !combat || !moveRef || !animator) return false;
        if (!cameraLockOn)
            cameraLockOn = FindFirstObjectByType<CameraLockOn>(FindObjectsInactive.Include);

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
        rapidVfxIndex = 0;

        attack.FreezeComboTimerFor(baseDuration + 0.1f);
        TagBus.Raise("Tag.Zoom");
        cameraLockOn?.SuppressLockOnZoom(3f);
        if (lockMoveDuringSkill) moveRef?.AddMovementLock(LOCK_RF, false, true);

        if (!string.IsNullOrEmpty(animStartTrigger)) animator.SetTrigger(animStartTrigger);
        if (!string.IsNullOrEmpty(animLoopBool)) animator.SetBool(animLoopBool, true);

        if (loopCo != null) StopCoroutine(loopCo);
        loopCo = StartCoroutine(Loop());
        return true;
    }

    public void OnPlayerHitInterrupt(PlayerHit.HitInterruptInfo info)
    {
        if (!isActive) return;
        isActive = false;
        if (loopCo != null) { StopCoroutine(loopCo); loopCo = null; }
        // 루프 중단: 피니시는 발동하지 않음
        if (!string.IsNullOrEmpty(animLoopBool)) animator?.SetBool(animLoopBool, false);
        // 이동락 사용 중이면 해제
        moveRef?.RemoveMovementLock("LOCK_RAPIDFIRE", false);
        lastCastEndTime = Time.time;
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

                if (elapsed >= maxDuration) { finishedFull = true; break; }
                if (stoppedMashing) { finishedFull = false; break; }
                attack.FreezeComboTimerFor(0.15f);
            }
            yield return null;
        }

        yield return StartCoroutine(EndRapidFireRoutine(finishedFull));
    }

    private IEnumerator EndRapidFireRoutine(bool fullFinish)
    {
        isActive = false;
        if (!string.IsNullOrEmpty(animLoopBool)) animator.SetBool(animLoopBool, false);

        if (fullFinish)
        {
            // 피니시: 선딜 → 히트 → 후딜
            if (!string.IsNullOrEmpty(animFinishTrigger)) animator.SetTrigger(animFinishTrigger);

            // 선딜 동안도 계속 이동 금지
            if (lockMoveDuringSkill) moveRef?.AddMovementLock(LOCK_RF, false, true);

            yield return new WaitForSeconds(finisherWindup);

            DoFinisherHit(); // x2 데미지 + 태그 + VFX

            yield return new WaitForSeconds(finisherRecovery);
        }
        else
        {
            if (!string.IsNullOrEmpty(animEndTrigger)) animator.SetTrigger(animEndTrigger);
        }

        lastCastEndTime = Time.time;
        if (lockMoveDuringSkill) moveRef?.RemoveMovementLock(LOCK_RF, false);
    }

    private void DoRapidHit()
    {
        var stats = (attack && attack.baseStats != null) ? attack.baseStats : new PlayerAttack.AttackBaseStats();
        float dmg = stats.baseDamage * rapidDamageMul;
        float knock = stats.baseKnockback * knockMul;
        float range = stats.baseRange * rangeMul;
        float radius = stats.baseRadius * radiusMul;

        Hitbox(dmg, knock, range, radius);
        TagBus.Raise("Tag.impact(S)");
        // VFX 0→1→2→3→4 순환
        SpawnVFX_Rapid(rapidVFX, ref rapidVfxIndex, rapidVfxOffset,
                       rapidVfxStartDelay, rapidVfxFadeIn, rapidVfxHold, rapidVfxFadeOut);
        rapidVfxIndex = (rapidVfxIndex + 1) % Mathf.Max(1, rapidVFX.Length);
    }

    private void DoFinisherHit()
    {
        var stats = (attack && attack.baseStats != null) ? attack.baseStats : new PlayerAttack.AttackBaseStats();
        float dmg = stats.baseDamage * finisherDamageMul;
        float knock = stats.baseKnockback * knockMul;
        float range = stats.baseRange * rangeMul;
        float radius = stats.baseRadius * radiusMul;

        Hitbox(dmg, knock, range, radius);

        OnTag?.Invoke(TAG_RAPID_FINISHER);
        TagBus.Raise("Tag.impact(L)");
        // 피니시 VFX
        SpawnVFX_One(finisherVFX, finisherVfxOffset,
                     finisherVfxStartDelay, finisherVfxFadeIn, finisherVfxHold, finisherVfxFadeOut);
        animator.SetBool("RapidFire_Loop", false);
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
        foreach (var h in hits)
        {
            if (!h) continue;
            int rid = h.transform.root.GetInstanceID();
            if (!_seenIds.Add(rid)) continue;

            var target = h.GetComponentInParent<IDamageable>();
            if (target != null)
            {
                Vector2 dir = ((Vector2)h.transform.position - (Vector2)combat.transform.position).normalized;
                target.ApplyHit(dmg, knock, dir, combat.gameObject);
            }
        }
    }

    // ---- VFX helpers (VFXFollowFlip 필요) ----
    private void SpawnVFX_Rapid(GameObject[] prefabs, ref int index, Vector2 offset,
        float startDelay, float fadeIn, float hold, float fadeOut)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        index = Mathf.Clamp(index, 0, prefabs.Length - 1);
        var fx = prefabs[index];
        SpawnVFX_One(fx, offset, startDelay, fadeIn, hold, fadeOut, rapidAttachToPlayer);
    }

    private void SpawnVFX_One(GameObject prefab, Vector2 offset,
        float startDelay, float fadeIn, float hold, float fadeOut, bool attach = true)
    {
        if (!prefab || !combat) return;
        StartCoroutine(SpawnVFXRoutine(prefab, offset, startDelay, fadeIn, hold, fadeOut, attach));
    }

    private IEnumerator SpawnVFXRoutine(GameObject prefab, Vector2 offset,
        float startDelay, float fadeIn, float hold, float fadeOut, bool attach)
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);
        var go = Instantiate(prefab, combat.transform.position, Quaternion.identity);
        if (attach) go.transform.SetParent(combat.transform, true);

        var follower = go.GetComponent<VFXFollowFlip>() ?? go.AddComponent<VFXFollowFlip>();
        follower.Init(combat.transform, PlayerSR, offset, attach);

        var sr = go.GetComponentInChildren<SpriteRenderer>();
        var cg = go.GetComponentInChildren<CanvasGroup>();

        if (sr != null)
        {
            Color c = sr.color; sr.color = new Color(c.r, c.g, c.b, 0f);
            if (fadeIn > 0f) { float t = 0f; while (t < fadeIn) { sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0f, 1f, t / fadeIn)); t += Time.deltaTime; yield return null; } }
            if (hold > 0f) yield return new WaitForSeconds(hold);
            if (fadeOut > 0f) { float t = 0f; while (t < fadeOut) { sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, t / fadeOut)); t += Time.deltaTime; yield return null; } }
            Destroy(go); yield break;
        }
        if (cg != null)
        {
            cg.alpha = 0f;
            if (fadeIn > 0f) { float t = 0f; while (t < fadeIn) { cg.alpha = Mathf.Lerp(0f, 1f, t / fadeIn); t += Time.deltaTime; yield return null; } }
            if (hold > 0f) yield return new WaitForSeconds(hold);
            if (fadeOut > 0f) { float t = 0f; while (t < fadeOut) { cg.alpha = Mathf.Lerp(1f, 0f, t / fadeOut); t += Time.deltaTime; yield return null; } }
            Destroy(go); yield break;
        }

        float total = Mathf.Max(0f, startDelay + fadeIn + hold + fadeOut);
        if (total > 0f) yield return new WaitForSeconds(total);
        Destroy(go);
    }

    private void OnDisable()
    {
        if (loopCo != null) StopCoroutine(loopCo);
        loopCo = null;
        isActive = false;
        if (!string.IsNullOrEmpty(animLoopBool) && animator) animator.SetBool(animLoopBool, false);
        moveRef?.RemoveMovementLock(LOCK_RF, false);
    }
}
