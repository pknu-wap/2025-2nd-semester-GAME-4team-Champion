using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public PlayerCombat playercombat;
    public Player_Heal playerheal;

    public Slider[] hpbar;    //플레이어 슬라이드바
    public Slider[] staminabar;

    public float maxhp = 100;   //플레이어 체력
    public float currenthp = 100;

    public float heal = 35;
    public float healdelay = 0.5f;
    public int healchance = 2;

    public float maxstamina = 100f;  //플레이어 스테미나
    public float currentstamina = 0f;
    public float playerstaminaregen = 2;


    public float reducestamina = 0; //가드시 스테미나 감소량
    public float gainhp= 0; //위빙 성공시 체력 회복


    public float regentime = 2f;    // 스테미나 회복 대기 시간
    private float lastactiontime;   //마지막으로 영향을 받은 시간


    public Slider[] enemyhpbar;   //적 슬라이드바
    public Slider[] enemystaminabar;
    public Image[] fillimage;   //플레이어 스테미나 *6, 플레이어 체력 * 3
    public Image[] enemyfillimage;   //적 스테미나 *6, 적 체력 * 3

    public float enemymaxhp = 100; //적 체력
    public float enemycurrenthp = 100;
    public float enemymaxstamina = 100f; //적 스테미나
    public float enemycurrentstamina = 0f;
    public float enemystaminaregen = 2;

    public float enemyregentime = 2f;    // 스테미나 회복 대기 시간
    public float enemylastactiontime;   //마지막으로 영향을 받은 시간

    void Start()
    {
        resetcurrenthp();
        resetcurrentstamina();

        resetenemystamina();

        matchHealPlayer();

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
            
            enemyfillimage[i].color = new Color(255 / 255f, (245 - enemycurrentstamina) / 255f, 57 / 255f, 1000 * (enemystaminabar[i].value - enemystaminabar[i].minValue));

            //fillimage[i].color = new Color(217/255f, (207-enemycurrentstamina)/255f, 28/255f, 10*enemycurrentstamina);
            //fillimage[i+2].color = new Color(105/255f, 107/255f, 30/255f, 10*enemycurrentstamina);

            //fillimage[5].color = new Color(167/255f, 171/255f, 0/255f, 10*currentstamina);
        }
    }

    public void getdamaged() //데미지 받음
    {

        playercombat.hp -= 20;
        resetcurrenthp();

        lastactiontime = Time.time;
    }

    public void HealPlayer(float amount)
    {
        
    }

    public void guard(int down) //가드 성공
    {
        if (currentstamina + down - reducestamina < maxstamina)
        {
            currentstamina += (down - reducestamina);
        }
        
        if (currentstamina > maxstamina)
        {
            currentstamina = maxstamina;
        }
        resetcurrentstamina();

        lastactiontime = Time.time;
    }

    public void justguard() //위빙(저스트 가드) 성공
    {
        currentstamina += 1;
        enemycurrentstamina += (30);
        if (currentstamina > maxstamina)
        {
            currentstamina = maxstamina;
        }
        if (enemycurrentstamina > enemymaxstamina)
        {
            enemycurrentstamina = enemymaxstamina;
        }

        if (currenthp + gainhp < maxhp) //위빙시 체력 회복
        {
            currenthp += gainhp;
            resetcurrenthp();
        }
        resetcurrentstamina();
        resetenemystamina();

        lastactiontime = Time.time;
    }




    private void resetcurrenthp() //체력 갱신
    {
        playercombat.matchingGM();
        float ratio = maxhp > 0f ? currenthp / maxhp : 0f;

        //플레이어 HP 슬라이더들 값만 갱신
        if (hpbar != null)
        {
            int count = hpbar.Length;
            for (int i = 0; i < count; i++)
            {
                if (hpbar[i] == null) continue;
                hpbar[i].value = ratio;
                Color c = fillimage[i+6].color;
                c.a = 1000 * (hpbar[i].value - hpbar[i].minValue);
                fillimage[i+6].color = c;
            }
        }

        if (currenthp > maxhp)
        {
            currenthp = maxhp;
        }
    }

    private void resetcurrentstamina() //스테미나 갱신
    {
        playercombat.matchingGM();
        for (int i = 0; i < 6; i++)
        {
            staminabar[i].value = currentstamina / maxstamina;
        }

    }

    private void resetenemystamina() //적 스테미나 갱신
    {
        for (int i = 0; i < 6; i++)
        {
            enemystaminabar[i].value = enemycurrentstamina / enemymaxstamina;
        }
    }

    private void resetenemyhp() //적 체력 갱신
    {
        float ratio = enemymaxhp > 0f ? enemycurrenthp / enemymaxhp : 0f;

        //적 HP 슬라이더들 값만 갱신
        if (enemyhpbar != null)
        {
            int count = enemyhpbar.Length;
            for (int i = 0; i < count; i++)
            {
                if (enemyhpbar[i] == null) continue;
                enemyhpbar[i].value = ratio;
                Color c = enemyfillimage[i+6].color;
                c.a = 1000 * (hpbar[i].value - enemyhpbar[i].minValue);
                enemyfillimage[i+6].color = c;
            }
        }
    }

    public void TakePlayerDamage(float amount)  //데미지 받음
    {
        playercombat.hp -= amount;
        if (currenthp < 0) currenthp = 0;
        resetcurrenthp();
        lastactiontime = Time.time;
        Debug.Log("Damage ->" + amount);
        // TODO: currenthp <= 0이면 사망 처리(리스폰/게임오버) 원하면 여기에 추가
    }



    //선택지로 인한 능력치 상승
    public void HpUp(float amount)  //체력 증가
    {
        maxhp += amount;
        currenthp += amount;
        if (currenthp > maxhp)
        {
            currenthp = maxhp;
        }
        resetcurrenthp();
    }

    public void StaminaUp(float amount) //스테미나 증가
    {
        maxstamina += amount;
        resetcurrentstamina();
    }

    public void GuardStamina(float amount) //가드시 스테미나 증가량 감소
    {
        reducestamina += amount;
    }

    public void WeavingHeal(float amount)   //위빙 성공시 체력 회복
    {
        gainhp += amount;
    }

    public void StaminaRegen(float amount)  //스테미나 회복 속도 증가
    {
        playerstaminaregen += amount;
    }

    public void MoreHeal(float amount)  //기합 회복량 증가
    {
        heal += amount;
        matchHealPlayer();
    }

    public void ManyHealChance(float amount)    //기합 횟수 증가
    {
        healchance += 1;
        playerheal.chargesLeft += 1;
        matchHealPlayer();
    }

    public void MoreStaminaDamage(float amount) //공격시 적 스테미나 충전율 증가/ 추후에 코드 더보고
    {

    }

    



    /*

            
            
            { "공격시 적 스테미나 충전율 증가", () => { gamemanager.playerstaminaregen += 8; } },
            { "적 기절 성공시 체력 회복", () => { gamemanager.playerstaminaregen += 8; } },
            { "적 기절 시간 증가", () => { gamemanager.playerstaminaregen += 8; } },

            { "차지 속도 증가", () => { gamemanager.playerstaminaregen += 8; } },   //아직 콤보x

            { "공격 성공시 스킬 쿨감", () => { gamemanager.playerstaminaregen += 8; } },
            { "위빙 성공시 스킬 쿨감", () => { gamemanager.playerstaminaregen += 8; } },

            { "적 기절 성공시 스테미나 회복", () => { gamemanager.playerstaminaregen += 8; } }, */


    public void matchHealPlayer()   //Player_Heal 코드와 연결
    {
        playerheal.healDuration = healdelay;
        playerheal.healAmount = heal;
        playerheal.maxCharges = healchance;
    }

}
