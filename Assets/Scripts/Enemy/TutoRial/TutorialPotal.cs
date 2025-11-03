using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public class TutorialPotal : MonoBehaviour
{
    private bool isPlayerNear = false;
    public GameObject text;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            text.SetActive(true);
            isPlayerNear = true;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            text.SetActive(false);
            isPlayerNear = false;
        }
    }

    void Update()
    {
        if (isPlayerNear && Input.GetKeyDown(KeyCode.F))
        {
            SceneManager.LoadScene("Tutorial");
        }
    }
}
