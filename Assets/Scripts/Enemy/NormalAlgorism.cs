using UnityEngine;
using System.Collections;

public class NormalAlgorism : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private float AttackCooldown;
    [SerializeField] private Transform player;

    [Header("Dash (Melee)")]
    [SerializeField] private float dashSpeed;
    private float dashDuration;
    private float preWindup;

    [Header("Stop In Front Of Player")]
    [SerializeField] private float stopoffset;
    [SerializeField] private LayerMask playerLayer;

    [Header("Dash (Range)")]
    [SerializeField] private float retreatSpeed;
    [SerializeField] private float retreatDuration;
    private float desiredDistance;
    private float rangePreWindup;

    [Header("Range Projectile")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform firePoint;
    private int volleyCount;
    [SerializeField] private float volleyInterval;

    [Header("Remain")]
    private float attackTimer;
    private Rigidbody2D rb;
    private bool isActing;
    private Collider2D PlayerCol;
    [SerializeField] private float RecognizedArea;

    [SerializeField] private SpriteRenderer spriteRenderer; // 테스트
    [SerializeField] private Collider2D movementArea;

    void Start()
    {
        attackTimer = 0f;
        rb = GetComponent<Rigidbody2D>();
        if (player) PlayerCol = player.GetComponent<Collider2D>();
        rb.position = ClampInside(rb.position);
    }

    void Update()
    {
        if (!isActing)
        {
            attackTimer += Time.deltaTime;
            if (attackTimer >= AttackCooldown)
            {
                Attack_Way_Choice();
                attackTimer = 0f;
            }
        }
    }

    void FixedUpdate()
    {
        if (!isActing)
            AIMoveMent();
    }

    void Attack_Way_Choice()
    {
        int Attack_Way_Type = Random.Range(0, 2);
        switch (Attack_Way_Type)
        {
            case 0:
                Melee_Attack();
                break;
            case 1:
                Range_Attack();
                break;
        }
    }

    void Melee_Attack()
    {
        int Melee_Attack_Type = Random.Range(0, 3);
        switch (Melee_Attack_Type)
        {
            case 0:
                Debug.Log("근거리 공격1: 짧게 돌진");
                preWindup = 2f;
                dashDuration = 0.5f;
                StartCoroutine(DashBurstStopInFront());
                break;
            case 1:
                Debug.Log("근거리 공격2: 약간 돌진");
                preWindup = 3f;
                dashDuration = 1.0f;
                StartCoroutine(DashBurstStopInFront());
                break;
            case 2:
                Debug.Log("근거리 공격3: 길게 돌진");
                preWindup = 4f;
                dashDuration = 1.5f;
                StartCoroutine(DashBurstStopInFront());
                break;
        }
    }

    void Range_Attack()
    {
        int Range_Attack_Type = Random.Range(0, 3);
        switch (Range_Attack_Type)
        {
            case 0:
                Debug.Log("원거리 공격: 후퇴 후 단발 ( 공격 차징 시간 1.0 )");
                desiredDistance = Mathf.Max(desiredDistance, 5f);
                rangePreWindup = 1.0f;
                volleyCount = 1;
                StartCoroutine(RetreatThenFire());
                break;
            case 1:
                Debug.Log("원거리 공격: 후퇴 후 2연발 ( 공격 차징 시간 1.3 )");
                desiredDistance = Mathf.Max(desiredDistance, 7f);
                rangePreWindup = 1.3f;
                volleyCount = 2;
                StartCoroutine(RetreatThenFire());
                break;
            case 2:
                Debug.Log("원거리 공격: 후퇴 후 3연발 ( 공격 차징 시간 1.5 )");
                desiredDistance = Mathf.Max(desiredDistance, 9f);
                rangePreWindup = 1.5f;
                volleyCount = 3;
                StartCoroutine(RetreatThenFire());
                break;
        }
    }

    IEnumerator DashBurstStopInFront()
    {
        isActing = true;
        rb.linearVelocity = Vector2.zero;
        if (spriteRenderer) spriteRenderer.color = Color.green; // 테스트

        yield return new WaitForSeconds(preWindup);

        Vector2 startPosition = rb.position;
        Vector2 dir = ((Vector2)player.position - startPosition).normalized;
        Vector2 target = ComputeFrontTarget(startPosition, dir);
        target = ClampInside(target);

        float timer = 0f;
        while (timer < dashDuration)
        {
            if (spriteRenderer) spriteRenderer.color = Color.blue; // 테스트

            Vector2 toTarget = target - rb.position;
            float dist = toTarget.magnitude;
            if (dist <= 0.02f) break;

            Vector2 toTargetDir = (dist > 1e-6f) ? toTarget / dist : Vector2.zero;
            float step = dashSpeed * Time.fixedDeltaTime;
            Vector2 nextPos = (step >= dist) ? target : rb.position + toTargetDir * step;
            nextPos = ClampInside(nextPos);

            rb.MovePosition(nextPos);
            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.linearVelocity = Vector2.zero;
        if (spriteRenderer) spriteRenderer.color = Color.red; // 테스트
        isActing = false;
    }

    Vector2 ComputeFrontTarget(Vector2 start, Vector2 dir)
    {
        float maxDist = 50f;
        Vector2 fallback = (Vector2)player.position - dir * stopoffset;

        RaycastHit2D hitPlayer = Physics2D.Raycast(start, dir, maxDist, playerLayer);
        if (hitPlayer.collider != null)
        {
            return hitPlayer.point - dir * stopoffset;
        }
        return fallback;
    }

    void FireOneProjectile()
    {
        if (projectilePrefab && firePoint)
            Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
    }

    IEnumerator RetreatThenFire()
    {
        isActing = true;
        if (spriteRenderer) spriteRenderer.color = Color.green; // 테스트

        float t = 0f;
        while (t < retreatDuration)
        {
            Vector2 toPlayer = (Vector2)player.position - rb.position;
            float dist = toPlayer.magnitude;

            Vector2 dirAway = (-toPlayer).normalized;
            float step = retreatSpeed * Time.fixedDeltaTime;

            Vector2 nextPos = rb.position + dirAway * step;
            nextPos = ClampInside(nextPos);
            rb.MovePosition(nextPos);

            if (desiredDistance > 0f && dist >= desiredDistance)
                break;

            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.linearVelocity = Vector2.zero;
        if (rangePreWindup > 0f) yield return new WaitForSeconds(rangePreWindup);

        if (spriteRenderer) spriteRenderer.color = Color.blue; // 테스트
        for (int i = 0; i < volleyCount; i++)
        {
            FireOneProjectile();
            if (i < volleyCount - 1 && volleyInterval > 0f)
                yield return new WaitForSeconds(volleyInterval);
        }

        if (spriteRenderer) spriteRenderer.color = Color.red; // 테스트
        isActing = false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.transform == player)
        {
            StopInFrontOfPlayer();
        }
    }

    void StopInFrontOfPlayer()
    {
        Vector2 dir = ((Vector2)player.position - rb.position).normalized;
        Vector2 stopPos = (Vector2)player.position - dir * stopoffset;
        rb.position = ClampInside(stopPos);
        rb.linearVelocity = Vector2.zero;
        isActing = false;
    }

    Vector2 ClampInside(Vector2 p)
    {
        if (!movementArea) return p;

        Vector2 closest = movementArea.ClosestPoint(p);
        Vector2 center = (Vector2)movementArea.bounds.center;
        Vector2 inward = (center - closest).sqrMagnitude > 1e-8f ? (center - closest).normalized : Vector2.zero;

        return closest + inward * 0.14f;
    }

    public void AIMoveMent()
    {
        Vector2 toPlayer = (Vector2)player.position - rb.position;
        float dist = toPlayer.magnitude;

        if (dist <= RecognizedArea)
        {
            Vector2 dir = toPlayer.sqrMagnitude > 1e-8f ? toPlayer.normalized : Vector2.zero;
            float speed = 3.0f;
            rb.linearVelocity = dir * speed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}