using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public Slider[] hpbar;    //플레이어 슬라이드바
    public Slider[] staminabar;
    public float maxhp = 100;   //플레이어 체력
    public float currenthp = 100;
    public float maxstamina = 100f;  //플레이어 스테미나
    public float currentstamina = 0f;
    public float playerstaminaregen = 2;

    public List<int> playerpower = new List<int> { 0, 0 };    //플레이어 레벨업 선택시 능력치(저스트 가드시 적 스테미나, 가드시 스테미나 감소)

    public float regentime = 2f;    // 스테미나 회복 대기 시간
    private float lastactiontime;   //마지막으로 영향을 받은 시간


    public Slider enemyhpbar;   //적 슬라이드바
    public Slider[] enemystaminabar;
    public Image[] fillimage;   //적 슬라이드바*2, 적 슬라이드바 배경*2, 플레이어 슬라이드바, 플레이어 슬라이드바 배경 순
    public float enemymaxhp = 100; //적 체력
    public float enemycurrenthp = 100;
    public float enemymaxstamina = 100f; //적 스테미나
    public float enemycurrentstamina = 0f;
    public float enemystaminaregen = 2;

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
            currentstamina -= regentime * Time.deltaTime * playerstaminaregen;
            if (currentstamina < 0)
            {
                currentstamina = 0;
            }

            resetcurrentstamina();
        }

        if (Time.time - enemylastactiontime >= enemyregentime && enemycurrentstamina > 0) //적 스테미나 감소
        {
            enemycurrentstamina -= enemyregentime * Time.deltaTime * enemystaminaregen;
            if (enemycurrentstamina < 0)
            {
                enemycurrentstamina = 0;
            }

            resetenemystamina();
        }

        for (int i = 0; i < 6; i++)
        {
            fillimage[i].color = new Color(255 / 255f, (245 - currentstamina) / 255f, 57 / 255f, 1000 * (staminabar[i].value - staminabar[i].minValue));


            //fillimage[i].color = new Color(217/255f, (207-enemycurrentstamina)/255f, 28/255f, 10*enemycurrentstamina);
            //fillimage[i+2].color = new Color(105/255f, 107/255f, 30/255f, 10*enemycurrentstamina);

            //fillimage[5].color = new Color(167/255f, 171/255f, 0/255f, 10*currentstamina);
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
        currentstamina += (20 + playerpower[1]);
        if (currentstamina > maxstamina)
        {
            currentstamina = maxstamina;
        }
        resetcurrentstamina();

        lastactiontime = Time.time;
    }

    public void justguard() //저스트 가드 성공
    {
        currentstamina += 1;
        enemycurrentstamina += (30 + playerpower[0]);
        if (currentstamina > maxstamina)
        {
            currentstamina = maxstamina;
        }
        if (enemycurrentstamina > enemymaxstamina)
        {
            enemycurrentstamina = enemymaxstamina;
        }

        resetcurrentstamina();
        resetenemystamina();

        lastactiontime = Time.time;
    }



    private void resetcurrenthp() //체력 갱신
    {
        float ratio = maxhp > 0f ? currenthp / maxhp : 0f;

        // 1) 플레이어 HP 슬라이더들 값만 안전하게 갱신
        if (hpbar != null)
        {
            int count = hpbar.Length;
            for (int i = 0; i < count; i++)
            {
                if (hpbar[i] == null) continue;
                hpbar[i].value = ratio;
            }
        }
    }

    private void resetcurrentstamina() //스테미나 갱신
    {
        for (int i = 0; i < 6; i++)
        {
            staminabar[i].value = currentstamina / maxstamina;
        }

    }

    private void resetenemystamina() //적 스테미나 갱신
    {
        enemystaminabar[0].value = enemycurrentstamina / enemymaxstamina;
        enemystaminabar[1].value = enemycurrentstamina / enemymaxstamina;
    }

    public void TakePlayerDamage(float amount)
    {
        currenthp -= amount;
        if (currenthp < 0) currenthp = 0;
        resetcurrenthp();
        lastactiontime = Time.time;
        Debug.Log("Damage ->" + amount);
        // TODO: currenthp <= 0이면 사망 처리(리스폰/게임오버) 원하면 여기에 추가
    }
}
