using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class DayTimer : MonoBehaviour
{
    public TextMeshProUGUI timertext;
    private Coroutine timerCoroutine;

    public float remaintime;    //남은 시간
    private float setting_time = 1800f; //타이머 세팅 값

    private float animaccumulator = 0f;
    
    public Animator animator;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ResetTimer();
        StartTimer();
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void StartTimer()    //타이머 시작
    {
        timerCoroutine = StartCoroutine(TimerStart());
    }

    public void StopTimer()    //타이머 정지
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }
    }

    public void ResetTimer()    //타이머 리셋
    {
        remaintime = setting_time;

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }

        //UpdateTimerText();
    }

    IEnumerator TimerStart()    //시간 계산
    {
        while(remaintime > 0)
        {
            remaintime -= Time.deltaTime*10;
            animaccumulator += Time.deltaTime*10;

            if (animaccumulator >= 60f)
            {
                animator.SetTrigger("next_time");
                animaccumulator -= 60f; 

                //UpdateTimerText();
            }

            
            yield return null;
        }
        remaintime = 0;
        timertext.text = $"D-day";
    }

    /*private void UpdateTimerText()  //시간 표시
    {
        int min = Mathf.FloorToInt(remaintime/60);
        
        timertext.text = $"D-{min+1}\n(M)";
    }*/

}
