using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public Slider hpbar;    //플레이어 슬라이드바
    public Slider staminabar;
    public float maxhp = 100;   //플레이어 체력
    public float currenthp = 100;
    public float maxstamina = 100;  //플레이어 스테미나
    public float currentstamina = 100;

    public float regentime = 2f;    // 스테미나 회복 대기 시간
    private float lastactiontime;   //마지막으로 영향을 받은 시간


    public Slider enemyhpbar;   //적 슬라이드바
    public Slider enemystaminabar;
    public float enemymaxhp = 100; //적 체력
    public float enemycurrenthp = 100;
    public float enemymaxstamina = 100; //적 스테미나
    public float enemycurrentstamina = 100;

    public float enemyregentime = 2f;    // 스테미나 회복 대기 시간
    private float enemylastactiontime;   //마지막으로 영향을 받은 시간

    void Start()    
    {
        resetcurrenthp();
        resetcurrentstamina();

        resetenemystamina();

        lastactiontime = Time.time;
    }

    void Update()
    {
        if (Time.time - lastactiontime >= regentime && currentstamina < maxstamina)
        {
            currentstamina += regentime * Time.deltaTime;
            if (currentstamina > maxstamina) 
                currentstamina = maxstamina;

            resetcurrentstamina();
        }

        if (Time.time - enemylastactiontime >= enemyregentime && enemycurrentstamina < enemymaxstamina)
        {
            enemycurrentstamina += enemyregentime * Time.deltaTime;
            if (enemycurrentstamina > enemymaxstamina) 
                enemycurrentstamina = enemymaxstamina;

            resetenemystamina();
        }
    }

    public void getdamaged() //데미지 받음
    {
        currenthp -= 20;
        resetcurrenthp();

        lastactiontime = Time.time;
    }

    public void guard() //가드 성공
    {
        currentstamina -= 20;
        resetcurrentstamina();

        lastactiontime = Time.time;
    }

    public void justguard() //저스트 가드 성공
    {
        currentstamina -= 1;
        enemycurrentstamina -= 30;

        resetcurrentstamina();
        resetenemystamina();

        lastactiontime = Time.time;
    }

    

    private void resetcurrenthp() //체력 갱신
    {
        hpbar.value = currenthp/maxhp;
    }
    
    private void resetcurrentstamina() //스테미나 갱신
    {
        staminabar.value = currentstamina/maxstamina;
    }

    private void resetenemystamina() //적 스테미나 갱신
    {
        enemystaminabar.value = enemycurrentstamina/enemymaxstamina;
    }
}
