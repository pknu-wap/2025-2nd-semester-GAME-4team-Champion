using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpeedPadEffect : MonoBehaviour
{
    [SerializeField] private PlayerMoveBehaviour moveRef;

    private SpeedPadSettings settings;
    private float currentScale = 1f;
    private bool inZone;
    private Coroutine exitCo;

    private Keyboard kb;

    private void Reset()
    {
        moveRef = GetComponent<PlayerMoveBehaviour>();
    }

    private void Awake()
    {
        if (!moveRef) moveRef = GetComponent<PlayerMoveBehaviour>();
        kb = Keyboard.current;
    }

    private void OnEnable()
    {
        currentScale = 1f;
        ApplyScale();
    }

    public void EnterZone(SpeedPadSettings s)
    {
        settings = s;
        inZone = true;
        if (exitCo != null) { StopCoroutine(exitCo); exitCo = null; }
        currentScale = Mathf.Max(currentScale, Mathf.Max(1f, settings.minScale));
        ApplyScale();
        TagBus.Raise("Tag.Field.SpeedPad.Enter");
    }

    public void ExitZone()
    {
        inZone = false;
        if (exitCo != null) StopCoroutine(exitCo);
        float dur = settings ? Mathf.Max(0.05f, settings.exitEaseTime) : 0.8f;
        exitCo = StartCoroutine(EaseBackTo1(dur));
        TagBus.Raise("Tag.Field.SpeedPad.Exit");
    }

    private void Update()
    {
        if (!inZone || settings == null) return;

        if (kb != null && kb.qKey.wasPressedThisFrame)
        {
            currentScale = Mathf.Min(settings.maxScale, currentScale + settings.mashStep);
            ApplyScale();
        }

        if (settings.mashDecayPerSec > 0f && currentScale > settings.minScale)
        {
            currentScale = Mathf.MoveTowards(currentScale, settings.minScale, settings.mashDecayPerSec * Time.unscaledDeltaTime);
            ApplyScale();
        }
    }

    private IEnumerator EaseBackTo1(float dur)
    {
        float start = currentScale;
        float t = 0f;
        while (t < dur)
        {
            float u = t / dur;
            SetScale(Mathf.Lerp(start, 1f, u));
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        SetScale(1f);
        exitCo = null;
    }

    private void ApplyScale() => SetScale(currentScale);

    private void SetScale(float s)
    {
        currentScale = Mathf.Max(1f, s);
        moveRef?.SetGuardSpeedScale(currentScale);
    }
}
