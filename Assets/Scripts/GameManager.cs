using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public Slider hpbar;    //플레이어 슬라이드바
    public Slider staminabar;
    public float hp = 100;
    float maxhp = 100;   //플레이어 체력
    float currenthp = 100;
    public float maxstamina = 100f;  //플레이어 스테미나
    public float currentstamina = 0f;

    public float regentime = 2f;    // 스테미나 회복 대기 시간
    private float lastactiontime;   //마지막으로 영향을 받은 시간


    public Slider enemyhpbar;   //적 슬라이드바
    public Slider enemystaminabar;
    public float enemymaxhp = 100; //적 체력
    public float enemycurrenthp = 100;
    public float enemymaxstamina = 100f; //적 스테미나
    public float enemycurrentstamina = 0f;

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
        if (Time.time - lastactiontime >= regentime && currentstamina > 0) //나의 스테미나 줄어드는 속도
        {
            currentstamina -= regentime * Time.deltaTime;
            if (currentstamina < 0) 
                {
                    currentstamina = 0;
                }

            resetcurrentstamina();
        }

        if (Time.time - enemylastactiontime >= enemyregentime && enemycurrentstamina > 0) //적 스테미나 감소
        {
            enemycurrentstamina -= enemyregentime * Time.deltaTime;
            if (enemycurrentstamina < 0) 
                {
                    enemycurrentstamina = 0;
                }

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
        currentstamina += 20;
        resetcurrentstamina();

        lastactiontime = Time.time;
    }

    public void justguard() //저스트 가드 성공
    {
        currentstamina += 1;
        enemycurrentstamina += 30;

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
