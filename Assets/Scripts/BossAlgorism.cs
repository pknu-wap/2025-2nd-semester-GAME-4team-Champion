using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BossAlgorism : MonoBehaviour
{
    [Header("Common")]
    public float AttackCooldown;
    public Transform player;

    [Header("Dash (Melee)")]
    public float dashSpeed;
    public float dashDuration;
    private float preWindup;

    [Header("Stop In Front Of Player")]
    public float stopoffset;
    public LayerMask playerLayer;

    [Header("Dash (Range)")]
    public float retreatSpeed;
    public float retreatDuration;
    public float desiredDistance;
    public float rangePreWindup;

    [Header("Remain")]
    public float attackTimer;
    private Rigidbody2D rb;
    private bool isActing;
    private Collider2D PlayerCol;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        attackTimer = 0f;
        PlayerCol = player.GetComponent<Collider2D>();
    }

    void Update()
    {
        if (player == null) return;

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
                Debug.Log("근거리 공격1: 플레이어에게 돌진 ( preWindup = 2 )");
                preWindup = 2f;
                StartCoroutine(DashBurstStopInFront());
                break;
            case 1:
                Debug.Log("근거리 공격2: 플레이어에게 돌진( preWindup = 3 )");
                preWindup = 3f;
                StartCoroutine(DashBurstStopInFront());
                break;
            case 2:
                Debug.Log("근거리 공격3: 플레이어에게 돌진( preWindup = 4 )");
                preWindup = 4f;
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
                Debug.Log("원거리 공격1: 플레이어로부터 멀어짐 ( preWindup = 2 )");
                rangePreWindup = 2f;
                StartCoroutine(RangeBurst());
                break;
            case 1:
                Debug.Log("원거리 공격2: 플레이어로부터 멀어짐 ( preWindup = 3 )");
                rangePreWindup = 3f;
                StartCoroutine(RangeBurst());
                break;
            case 2:
                Debug.Log("원거리 공격3: 플레이어로부터 멀어짐 ( preWindup = 4 )");
                rangePreWindup = 4f;
                StartCoroutine(RangeBurst());
                break;
        }
    }

    System.Collections.IEnumerator DashBurstStopInFront()
    {
        isActing = true; 
        rb.linearVelocity = Vector2.zero;
        //애니메이션 넣는 구간 ( 스킬 사용 전 준비 동작 )
        if (preWindup > 0f) yield return new WaitForSeconds(preWindup);

        Vector2 start = rb.position;
        Vector2 dir = ((Vector2)player.position - start).normalized; // *(Vector2) 필요 + 정규화 필요
        Vector2 target = ComputeFrontTarget(start, dir);

        float timer = 0f;
        while (timer < dashDuration)
        {
            Vector2 ToTarget = target - rb.position;
            float dist = ToTarget.magnitude; // 벡터의 크기 = 거리
            
            if (dist <= 0.02f)
            {
                break;
            }

            Vector2 ToTargetDir = ToTarget / (dist + 1e-6f); //보정값( 0으로 나뉘는 것을 방지 )
            float step = dashSpeed * Time.fixedDeltaTime;
            // 감사합니다. 지피티님 ( 검나 어렵네 ㅆ... )
            Vector2 nextPos = (step >= dist) ? target : rb.position + ToTargetDir * step;

            rb.MovePosition(nextPos);

            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.linearVelocity = Vector2.zero;
        isActing = false;
    }

    // 우리는 AI 시대에 살고 있따;;
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


    System.Collections.IEnumerator RangeBurst()
    {
        if (player == null) yield break;
        isActing = true;

        // 1) 플레이어와의 거리 확인 후 필요하면 후퇴
        float timer = 0f;
        while (timer < retreatDuration)
        {
            Vector2 toPlayer = (Vector2)player.position - rb.position;
            if (toPlayer.magnitude >= desiredDistance) break; // 원하는 거리 확보되면 중단

            // 매 프레임 반대 방향으로 물러나기 (항상 최신 방향으로 갱신)
            Vector2 dirAway = (-toPlayer).normalized;
            float step = retreatSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + dirAway * step);

            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.linearVelocity = Vector2.zero;

        if (rangePreWindup > 0f)
            yield return new WaitForSeconds(rangePreWindup);

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
        Vector2 StopPos = (Vector2)player.position - dir * stopoffset;
        rb.position = StopPos;
        rb.linearVelocity = Vector2.zero;
        isActing = false;
    }
}
