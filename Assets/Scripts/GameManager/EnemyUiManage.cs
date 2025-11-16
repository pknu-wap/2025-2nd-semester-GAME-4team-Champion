using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EnemyUiManage : MonoBehaviour
{
    public Slider[] enemyhpbar;   //적 슬라이드바
    public Slider[] enemystaminabar;
   
    public Image[] enemyfillimage;   //적 스테미나 *6, 적 체력 * 3

    private float enemymaxhp = 100; //적 체력
    private float enemycurrenthp = 100;
    private float enemymaxstamina = 100f; //적 스테미나
    private float enemycurrentstamina = 0f;
    private float enemystaminaregen = 2;

    private float enemyregentime = 2f;    // 스테미나 회복 대기 시간
    private float enemylastactiontime;   //마지막으로 영향을 받은 시간

    [SerializeField]private BossCore Bc;
    
    void Start()
    {
        resetenemystamina();


        enemylastactiontime = Time.time;
    }


    void Update()
    {
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
            
            enemyfillimage[i].color = new Color(255 / 255f, (245 - enemycurrentstamina) / 255f, 57 / 255f, 1000 * (enemystaminabar[i].value - enemystaminabar[i].minValue));

            //fillimage[i].color = new Color(217/255f, (207-enemycurrentstamina)/255f, 28/255f, 10*enemycurrentstamina);
            //fillimage[i+2].color = new Color(105/255f, 107/255f, 30/255f, 10*enemycurrentstamina);

            //fillimage[5].color = new Color(167/255f, 171/255f, 0/255f, 10*currentstamina);
        }
    }

    public void pjustguard(float amount)    //플레이어 저스트 가드
    {
        enemycurrentstamina += amount;
        if (enemycurrentstamina > enemymaxstamina)
        {
            enemycurrentstamina = enemymaxstamina;
        }

        resetenemystamina();

    }

    private void resetenemystamina() //적 스테미나 갱신
    {
        
        enemycurrentstamina = Bc.CurrentStamina;

        if (enemycurrentstamina < 0)
        {
            enemycurrentstamina = 0;
        }
        for (int i = 0; i < 6; i++)
        {
            enemystaminabar[i].value = enemycurrentstamina / enemymaxstamina;
        }
    }

    private void resetenemyhp() //적 체력 갱신
    {
        enemycurrenthp = Bc.CurrentHp;
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
                c.a = 1000 * (enemyhpbar[i].value - enemyhpbar[i].minValue);
                enemyfillimage[i+6].color = c;
            }
        }
    }

    public void LinkHp()
    {
        resetenemyhp();
    }
    public void LinkStamina()
    {
        resetenemystamina();
    }
}
