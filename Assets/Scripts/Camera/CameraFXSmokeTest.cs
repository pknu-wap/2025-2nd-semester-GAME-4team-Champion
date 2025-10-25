using UnityEngine;

public class CameraFXSmokeTest : MonoBehaviour
{
    [Header("Hook")]
    public CameraFXController fx;
    public CameraFXProfile profile1;  // ¿¹: FX_PowerStrike
    public CameraFXProfile profile2;  // ¿¹: FX_ParrySuccess

    [Header("Keys")]
    public KeyCode keyProfile1 = KeyCode.Alpha4;
    public KeyCode keyProfile2 = KeyCode.Alpha5;
    public KeyCode keyReset = KeyCode.R;

    private void Update()
    {
        if (!fx) return;

        if (Input.GetKeyDown(keyProfile1) && profile1) fx.PlayProfile(profile1);
        if (Input.GetKeyDown(keyProfile2) && profile2) fx.PlayProfile(profile2);
        if (Input.GetKeyDown(keyReset)) fx.ResetZoom(0.2f);
    }

    private void OnGUI()
    {
        if (!fx) return;
        GUI.Label(new Rect(8, 8, 300, 24), $"OrthoSize: {fx.GetCurrentSize():0.00}");
    }
}
