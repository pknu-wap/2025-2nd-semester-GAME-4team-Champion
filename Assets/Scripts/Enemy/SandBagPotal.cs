using UnityEngine;
using System.Collections;

public class SandBagPotal : MonoBehaviour
{
    public string playerTag = "Player";
    public Transform target;
    public GameObject sandbag;
    public GameObject SandBagCanvas;
    public CameraLockOn camFollow;
    public Transform player;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            other.transform.position = target.position;
            StartSandbagMiniGame();
        }
    }

    private void StartSandbagMiniGame()
    {
        SandBagCanvas.SetActive(true);
        player.GetComponent<PlayerMoveBehaviour>().enabled = false;
    }
    public void ClickStartBTN()
    {
        sandbag.SetActive(true);
        SandBagCanvas.SetActive(false);
    }

    public void EndSandbagMiniGame()
    {
        if (camFollow != null && player != null)
        {
            camFollow.player = player;
            player.GetComponent<PlayerMoveBehaviour>().enabled = true;
        }
    }
}
