using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class Changer : MonoBehaviour
{
    [SerializeField] private string sceneToLoad;    //씬 이름
    [SerializeField] private float loadingtime = 1f;
    [SerializeField] private Image fadeImage;   
    [SerializeField] private float fadeDuration = 1f; // 페이드 속도

    public void ChangeScene()
    {
        Debug.Log("버튼 눌러짐");
        StartCoroutine(FadeAndLoad());
    }

    private IEnumerator FadeAndLoad()
    {
        yield return StartCoroutine(FadeOut()); //페이드 아웃 실행

        yield return new WaitForSeconds(loadingtime);   //대기

        SceneManager.LoadScene(sceneToLoad, LoadSceneMode.Single);  //씬 넘기기
    }

    private IEnumerator FadeOut()   //페이드 아웃
    {
        float waittime = 0f;
        Color color = fadeImage.color;

        while (waittime < fadeDuration)
        {
            waittime += Time.deltaTime;
            color.a = Mathf.Clamp01(waittime / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        color.a = 1f;
        fadeImage.color = color;
    }
}

