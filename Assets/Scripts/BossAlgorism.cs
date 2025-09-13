using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BossAlgorism : MonoBehaviour
{
    [Header("Common")]
    public float AttackCooldown = 2f;
    public Transform player;

    [Header("Dash (Melee)")]
    public float dashSpeed = 25f;
    public float dashDuration = 0.15f;
    public float preWindup = 0.08f;

    [Header("Stop In Front")]
    public float stopOffset = 0.6f;
    public LayerMask playerLayer;
    public LayerMask obstacleMask;

    [Header("Range")]
    public float retreatSpeed = 12f;
    public float retreatDuration = 0.15f;
    public float desiredDistance = 3.0f;

    public float attackTimer;
    private Rigidbody2D rb;
    private bool isActing;
    private Collider2D playerCol;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        attackTimer = 0f;
        if (player != null) playerCol = player.GetComponent<Collider2D>();
        // 충돌 누락 방지
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Update()
    {
        if (player == null) return;

        attackTimer += Time.deltaTime;
        if (attackTimer >= AttackCooldown && !isActing)
        {
            Attack_Way_Choice();
            attackTimer = 0f;
        }
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
                Debug.Log("근거리 공격1: 플레이어에게 돌진");
                StartCoroutine(DashBurstStopInFront());
                break;
            case 1:
                Debug.Log("근거리 공격2: 플레이어에게 돌진");
                StartCoroutine(DashBurstStopInFront());
                break;
            case 2:
                Debug.Log("근거리 공격3: 플레이어에게 돌진");
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
                Debug.Log("원거리 공격1: 플레이어로부터 멀어짐");
                StartCoroutine(RetreatBurst());
                break;
            case 1:
                Debug.Log("원거리 공격2: 플레이어로부터 멀어짐");
                StartCoroutine(RetreatBurst());
                break;
            case 2:
                Debug.Log("원거리 공격3: 플레이어로부터 멀어짐");
                StartCoroutine(RetreatBurst());
                break;
        }
    }

    System.Collections.IEnumerator DashBurstStopInFront()
    {
        if (player == null) yield break;
        isActing = true;

        // 0) 예비동작
        rb.linearVelocity = Vector2.zero;
        if (preWindup > 0f) yield return new WaitForSeconds(preWindup);

        // 1) 목표 지점 계산: 플레이어 콜라이더 앞 지점
        Vector2 start = rb.position;
        Vector2 dir = ((Vector2)player.position - start).normalized;
        Vector2 target = ComputeFrontTarget(start, dir);

        // 2) 목표 지점까지 순간 대시 (MovePosition으로 과속 안정)
        float timer = 0f;
        while (timer < dashDuration)
        {
            Vector2 toTarget = target - rb.position;
            float dist = toTarget.magnitude;
            if (dist <= 0.02f) break;

            Vector2 stepDir = toTarget / (dist + 1e-6f);
            float step = dashSpeed * Time.fixedDeltaTime;
            Vector2 nextPos = (step >= dist) ? target : rb.position + stepDir * step;

            rb.MovePosition(nextPos);

            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // 3) 정지
        rb.linearVelocity = Vector2.zero;
        isActing = false;
    }

    // 플레이어 바로 앞 '정지 목표점' 계산
    Vector2 ComputeFrontTarget(Vector2 start, Vector2 dir)
    {
        float maxDist = 50f;
        Vector2 fallback = (Vector2)player.position - dir * stopOffset;

        // 플레이어까지 Raycast
        RaycastHit2D hitPlayer = Physics2D.Raycast(start, dir, maxDist, playerLayer);
        if (hitPlayer.collider != null)
        {
            return hitPlayer.point - dir * stopOffset;
        }

        return fallback;
    }


    System.Collections.IEnumerator RetreatBurst()
    {
        if (player == null) yield break;
        isActing = true;

        float dist = Vector2.Distance(transform.position, player.position);
        if (dist < desiredDistance)
        {
            Vector2 dir = ((Vector2)transform.position - (Vector2)player.position).normalized;
            float timer = 0f;
            while (timer < retreatDuration)
            {
                Vector2 toPlayer = (Vector2)player.position - rb.position;
                if (toPlayer.magnitude >= desiredDistance) break;

                float step = retreatSpeed * Time.fixedDeltaTime;
                rb.MovePosition(rb.position + dir * step);

                timer += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        rb.linearVelocity = Vector2.zero;
        isActing = false;
    }

    // --- 안전장치: 실제로 부딪히면 즉시 '앞에서 스냅' ---
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActing || player == null) return;
        if (other.transform == player)
        {
            SnapInFrontOfPlayer();
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isActing || player == null) return;
        if (collision.transform == player)
        {
            SnapInFrontOfPlayer();
        }
    }

    void SnapInFrontOfPlayer()
    {
        Vector2 dir = ((Vector2)player.position - rb.position).normalized;
        Vector2 snapPos = (Vector2)player.position - dir * stopOffset;
        rb.position = snapPos;
        rb.linearVelocity = Vector2.zero;
        isActing = false;
    }
}
