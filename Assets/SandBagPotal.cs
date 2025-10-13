using UnityEngine;
using System.Collections;

public class SandBagPotal : MonoBehaviour
{
    public string playerTag = "Player";
    public Transform target;
    public GameObject sandbag;
    public CameraFollow camFollow;
    public Transform player;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            other.transform.position = target.position;
            StartCoroutine(StartSandbagMiniGame());
        }
    }

    IEnumerator StartSandbagMiniGame()
    {
        Debug.Log("잠시 후 게임을 시작합니다.");
        yield return new WaitForSeconds(3f);

        if (sandbag != null)
        {
            sandbag.SetActive(true);
        }
    }

    public void EndSandbagMiniGame()
    {
        if (camFollow != null && player != null)
        {
            camFollow.target = player;
        }
    }
}
