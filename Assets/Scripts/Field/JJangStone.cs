using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 부딪히면 잠깐 밀려나고 바로 멈추는 장애물.
/// 점프 중엔 무시.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class JJangStone : MonoBehaviour
{
    [Header("Trip Core")]
    [SerializeField, Min(0f)] private float tripDuration = 2.0f;       // 이동 불가 시간
    [SerializeField, Min(0f)] private float retriggerCooldown = 1.0f;  // 재트리거 대기
    [SerializeField] private string tripAnimTrigger = "Trip";           // 넘어짐 애니메이션 트리거 이름

    [Header("Impulse (Push)")]
    [Tooltip("부딪힐 때 한번에 밀쳐지는 힘 (즉발적)")]
    [SerializeField, Min(0f)] private float pushForce = 8f;
    [Tooltip("튕겨난 후 즉시 멈추기까지의 시간 (0.1~0.2초 권장)")]
    [SerializeField, Min(0f)] private float stopDelay = 0.15f;

    [Header("Filter")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private LayerMask triggerMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    // 내부 변수
    private readonly Dictionary<int, float> _nextAllowedTime = new();

    public const string TAG_TRIP_START = "Tag.Obstacle.Trip.Start";
    public const string TAG_TRIP_END = "Tag.Obstacle.Trip.End";
    public event System.Action<string> OnTag;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & triggerMask) == 0) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        var root = other.transform.root;
        var move = root.GetComponent<PlayerMoveBehaviour>();
        if (!move) return;

        var jump = root.GetComponent<Player_Jump>();
        if (jump != null && jump.IsJumping) return;

        int id = root.GetInstanceID();
        if (_nextAllowedTime.TryGetValue(id, out var t) && Time.time < t) return;

        _nextAllowedTime[id] = Time.time + retriggerCooldown;
        StartCoroutine(TripRoutine(root.gameObject, move));
    }

    private IEnumerator TripRoutine(GameObject playerRoot, PlayerMoveBehaviour move)
    {
        OnTag?.Invoke(TAG_TRIP_START);
        TagBus.Raise(TAG_TRIP_START);

        var anim = playerRoot.GetComponentInChildren<Animator>();
        if (anim && !string.IsNullOrEmpty(tripAnimTrigger))
            anim.SetTrigger(tripAnimTrigger);

        const string LOCK_TRIP = "OBSTACLE_TRIP";
        move?.AddMovementLock(LOCK_TRIP, hardFreezePhysics: false, zeroVelocity: true);

        var rb = playerRoot.GetComponent<Rigidbody2D>();
        if (rb)
        {
            Vector2 dir = Vector2.zero;

            // 플레이어의 이동 방향 계산
            if (move && move.LastFacing.sqrMagnitude > 0.001f)
                dir = move.LastFacing.normalized;
            else
                dir = (playerRoot.transform.position - transform.position).normalized;

            // 즉발적인 힘 추가 (Impulse)
            rb.linearVelocity = Vector2.zero; // 기존 속도 제거
            rb.AddForce(dir * pushForce, ForceMode2D.Impulse);

            if (logDebug) Debug.Log($"[Obstacle_Trip] push dir={dir}, force={pushForce}");

            // 짧은 시간 후 정지
            yield return new WaitForSeconds(stopDelay);
            rb.linearVelocity = Vector2.zero;
        }

        // 일정 시간 이동 불가 유지
        yield return new WaitForSeconds(tripDuration - stopDelay);

        move?.RemoveMovementLock(LOCK_TRIP, hardFreezePhysics: false);

        OnTag?.Invoke(TAG_TRIP_END);
        TagBus.Raise(TAG_TRIP_END);

        if (logDebug) Debug.Log("[Obstacle_Trip] trip end");
    }
}
