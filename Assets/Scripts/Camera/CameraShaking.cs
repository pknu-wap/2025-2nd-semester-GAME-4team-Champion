using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class CameraShaking : MonoBehaviour
{
    public float power;
    private CinemachineImpulseSource Impulse;
    int dir = 1;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Impulse = GetComponent<CinemachineImpulseSource>();
        CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            StartCoroutine(ImpulseMoveMent());
        }
    }
    
    public IEnumerator ImpulseMoveMent()
    {
        Impulse.GenerateImpulse(Vector3.right * dir * power);

        dir *= -1;
        yield return new WaitForSeconds(0.1f);
        Impulse.GenerateImpulse(Vector3.right * dir * power);
    }
}
