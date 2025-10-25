using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpeedPadZone : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private SpeedPadSettings settings;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private LayerMask triggerMask = ~0;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true; // 반드시 트리거
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & triggerMask) == 0) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        // 플레이어 루트에서 바로 효과 컴포넌트 획득
        var eff = other.transform.root.GetComponent<SpeedPadEffect>();
        if (!eff) return; // 미리 붙여두는 게 최적

        eff.EnterZone(settings);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & triggerMask) == 0) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        var eff = other.transform.root.GetComponent<SpeedPadEffect>();
        if (!eff) return;

        eff.ExitZone();
    }
}
