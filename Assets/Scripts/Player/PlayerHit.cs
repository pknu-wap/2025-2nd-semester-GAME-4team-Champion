using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerCombat combat;          // ü��/���¹̳�, ��������, �ִ� ��
    [SerializeField] private PlayerDefense defense;        // ����/����(�и�) �Ǵ� & ��
    [SerializeField] private PlayerAttack attack;
    [SerializeField] private PlayerMoveBehaviour moveRef;  // �̵���/�ٶ󺸴� ���� ����
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody2D rb;

    [Header("Hit Reaction (Config)")]
    [SerializeField] private float baseHitstun = 0.20f;   // �⺻ ����
    [SerializeField] private float blockHitstunMul = 0.5f;// ����� ���� ���
     
    [Header("Animation Variants")]
    [SerializeField] private int weavingVariants = 3;
    [SerializeField] private int hitVariants = 3;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    // === ���� ===
    private bool inHitstun = false;
    private float hitstunEndTime = 0f;
    private float iFrameEndTime = 0f;
    private Coroutine hitstunCo;

    public bool InHitstun => inHitstun;

    private void Awake()
    {
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!defense) defense = GetComponent<PlayerDefense>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!attack) attack = GetComponent<PlayerAttack>(); // �� �ٽ�
    }

    private void Reset()
    {
        if (!combat) combat = GetComponent<PlayerCombat>();
        if (!defense) defense = GetComponent<PlayerDefense>();
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        if (!animator) animator = GetComponent<Animator>();
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!attack) attack = GetComponent<PlayerAttack>();
    }

    private Vector2 FacingOrRight()
    {
        return (moveRef && moveRef.LastFacing.sqrMagnitude > 0f) ? moveRef.LastFacing : Vector2.right;
    }

    public void OnHit(float damage, float knockback, Vector2 hitDir, bool parryable, GameObject attacker = null, float hitstun = -1f)
    {
        if (combat.IsHealing) combat.CancelHealing();
        {
            if (Time.time < iFrameEndTime) return;

            combat.EnterCombat("GotHit");

            Vector2 facing = FacingOrRight();
            Vector2 inFrontToEnemy = -hitDir.normalized; // �÷��̾����
            if (defense && defense.IsBlocking)
            {
                if (moveRef == null || moveRef.LastFacing.sqrMagnitude < 0.0001f || Vector2.Dot(facing, inFrontToEnemy) < 0f)
                    facing = inFrontToEnemy;
            }

            // ���/���� �Ǵ�(���� ��/�и� ������� PlayerDefense�� ó��)
            var outcome = defense ? defense.Evaluate(facing, inFrontToEnemy, parryable) : DefenseOutcome.None;

            // === ����(�и�) ���� ===
            if (outcome == DefenseOutcome.Parry)
            {
                if (debugLogs) Debug.Log($"[WEAVING OK] t={Time.time:F2}s, attacker={(attacker ? attacker.name : "null")}");
                PlayRandomWeaving();


                var parryableTarget = attacker ? attacker.GetComponent<IParryable>() : null;
                parryableTarget?.OnParried(transform.position);

                float windowEnd = defense.LastBlockPressedTime + defense.ParryWindow;
                float lockDur = Mathf.Max(0f, (windowEnd + defense.WeavingPostHold) - Time.time);
                Debug.Log($"[HIT] Arming RIPOSTE window={lockDur:F2}s  (now={Time.time:F2})");
                defense.StartParryLock(lockDur, true);   // �̵���(�ӵ� 0 ����)
                defense.ForceBlockFor(lockDur);          // ���� ����
                attack?.ArmCounter(lockDur * 1.5f);
                // ª�� i-frame (���ϸ� ��ġ ����)
                iFrameEndTime = Time.time + 0.05f;
                return;
            }

            // === �Ϲ� ���� ���� ===
            if (outcome == DefenseOutcome.Block)
            {
                float finalDamage = damage * defense.BlockDamageMul;
                float finalKnock = knockback * defense.BlockKnockMul;

                combat.ApplyDamage(finalDamage);
                ApplyKnockbackXOnly(inFrontToEnemy, finalKnock);

                // ���¹̳� ���� & �극��ũ
                combat.AddStamina(-damage);
                if (combat.Stamina <= 0f) defense.TriggerStaminaBreak();
                else
                {
                    float stun = (hitstun >= 0f ? hitstun : baseHitstun) * blockHitstunMul;
                    StartHitstun(stun, playHitAnim: false);
                }

                animator?.SetTrigger("BlockHit");
                return;
            }

            // === ���� ����(��/�Ĺ�/����) ===
            combat.ApplyDamage(damage);
            ApplyKnockbackXOnly(inFrontToEnemy, knockback);
            combat.AddStamina(-damage * combat.StaminaLossOnHitMul);
            float stunRaw = (hitstun >= 0f ? hitstun : baseHitstun);
            StartHitstun(stunRaw, playHitAnim: true);
        }
    }

    // X�����θ� �˹�
    private void ApplyKnockbackXOnly(Vector2 dirToEnemy, float force)
    {
        if (!rb || force <= 0f) return;
        float x = -Mathf.Sign(dirToEnemy.x);
        if (Mathf.Abs(x) < 0.0001f)
            x = (moveRef && Mathf.Abs(moveRef.LastFacing.x) > 0.0001f) ? Mathf.Sign(moveRef.LastFacing.x) : 1f;

        rb.linearVelocity = new Vector2(x * force, 0f);
    }

    public void StartHitstun(float duration, bool playHitAnim = true)
    {
        if (duration <= 0f) return;

        float end = Time.time + duration;
        hitstunEndTime = Mathf.Max(hitstunEndTime, end);
        if (hitstunCo == null) hitstunCo = StartCoroutine(HitstunRoutine());

        // ������ �츮�� ���۸� ���
        moveRef?.SetMovementLocked(true, hardFreezePhysics: false, zeroVelocity: false);

        if (playHitAnim) PlayRandomHit();
    }

    private IEnumerator HitstunRoutine()
    {
        inHitstun = true;
        while (Time.time < hitstunEndTime) yield return null;
        inHitstun = false;
        moveRef?.SetMovementLocked(false, hardFreezePhysics: false);
        hitstunCo = null;
    }

    private void PlayRandomHit()
    {
        if (!animator || hitVariants <= 0) return;
        int idx = Mathf.Clamp(Random.Range(1, hitVariants + 1), 1, hitVariants);
        animator.SetTrigger($"Hit{idx}");
    }

    public void PlayRandomWeaving()
    {
        if (!animator) return;
        int max = Mathf.Max(1, weavingVariants);             // �ּ� 1
        int idx = Mathf.Clamp(Random.Range(1, max + 1), 1, max);

        animator.SetTrigger($"Weaving{idx}");

        // �� ��� ���� Weaving ��ȣ�� ���� ��ũ��Ʈ�� �Ѱ��ش�
        if (attack != null)
            attack.SetLastWeavingIndex(idx);
    }
}
