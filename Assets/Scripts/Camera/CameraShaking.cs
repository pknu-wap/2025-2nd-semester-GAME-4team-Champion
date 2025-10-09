using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CameraShaking : MonoBehaviour
{
    [SerializeField] private Animator Camera;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            StartCoroutine(CameraShake());
        }
    }

    public IEnumerator CameraShake()
    {
        Camera.SetTrigger("Shake");
        yield return null;
    }
}
