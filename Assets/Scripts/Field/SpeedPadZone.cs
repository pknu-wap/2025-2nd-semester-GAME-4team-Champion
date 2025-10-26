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
        col.isTrigger = true; // �ݵ�� Ʈ����
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & triggerMask) == 0) return;
        if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;

        // �÷��̾� ��Ʈ���� �ٷ� ȿ�� ������Ʈ ȹ��
        var eff = other.transform.root.GetComponent<SpeedPadEffect>();
        if (!eff) return; // �̸� �ٿ��δ� �� ����

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
