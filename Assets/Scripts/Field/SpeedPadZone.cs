using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpeedPadZone : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private SpeedPadSettings settings;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private LayerMask triggerMask = ~0;
    [SerializeField] private bool debugLog = false;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & triggerMask) == 0) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        // 🔧 루트가 아니라 '부모 어디에 있어도' 찾도록 변경
        var eff = other.GetComponentInParent<SpeedPadEffect>();
        if (!eff)
        {
            if (debugLog) Debug.LogWarning("[SpeedPadZone] 플레이어 쪽에 SpeedPadEffect가 없습니다.", this);
            return;
        }

        eff.OnEnterPad(settings);
        if (debugLog) Debug.Log("[SpeedPadZone] Enter → Effect.OnEnterPad 호출", this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & triggerMask) == 0) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        var eff = other.GetComponentInParent<SpeedPadEffect>();
        if (!eff) return;

        eff.OnExitPad();
        if (debugLog) Debug.Log("[SpeedPadZone] Exit → Effect.OnExitPad 호출", this);
    }
}
