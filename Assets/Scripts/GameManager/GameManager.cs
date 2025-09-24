using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [Header("Health")]
    // public Slider HpBar;
    // public Slider StaminaBar;
    public Slider EnemyHpBar;
    public Slider[] EnemyStaminaBars;
    public Image[] FillImages;

    [Header("Object")]
    // public GameObject PlayerObject;
    public GameObject EnemyObject;

    // private PlayerCombat Player;
    private BossCore Enemy;

    [Header("Stamina Settings")]
    public float EnemyStaminaRegen = 2f;
    public float EnemyRegenTime = 2f;

    private float EnemyLastActionTime;

    // private float PlayerHp01;
    // private float PlayerStamina01;

    private void Start()
    {
        // Player = PlayerObject.GetComponent<PlayerCombat>();
        Enemy = EnemyObject.GetComponent<BossCore>();

        // PlayerHp01 = Player.Hp01;
        // PlayerStamina01 = Player.Stamina01;

        // Player.OnHealthChanged += HandlePlayerHealthChanged;
        // Player.OnStaminaChanged += HandlePlayerStaminaChanged;

        // ResetPlayerHp();
        // ResetPlayerStamina();
        ResetEnemyHp();
        ResetEnemyStamina();

        EnemyLastActionTime = Time.time;
    }

    private void OnDestroy()
    {
        // if (Player != null)
        // {
        //     Player.OnHealthChanged -= HandlePlayerHealthChanged;
        //     Player.OnStaminaChanged -= HandlePlayerStaminaChanged;
        // }
    }

    private void Update()
    {
        if (Time.time - EnemyLastActionTime >= EnemyRegenTime && Enemy.CurrentStamina > 0f)
        {
            Enemy.CurrentStamina -= EnemyRegenTime * Time.deltaTime * EnemyStaminaRegen;
            if (Enemy.CurrentStamina < 0f)
            {
                Enemy.CurrentStamina = 0f;
            }
            ResetEnemyStamina();
        }

        float eNorm = (Enemy.MaxStamina > 0f) ? (Enemy.CurrentStamina / Enemy.MaxStamina) : 0f;
        // float pNorm = PlayerStamina01;

        for (int i = 0; i < 2; i++)
        {
            FillImages[i].color     = new Color(217 / 255f, (207 - Enemy.CurrentStamina) / 255f, 28 / 255f, 10f * eNorm);
            FillImages[i + 2].color = new Color(105 / 255f, 107 / 255f, 30 / 255f, 10f * eNorm);
            // FillImages[4].color   = new Color(1f, (245 - pNorm * 255f) / 255f, 57 / 255f, 10f * pNorm);
            // FillImages[5].color   = new Color(167 / 255f, 171 / 255f, 0f, 10f * pNorm);
        }
    }

    // private void HandlePlayerHealthChanged(float current, float max)
    // {
    //     PlayerHp01 = (max > 0f) ? current / max : 0f;
    //     ResetPlayerHp();
    // }

    // private void HandlePlayerStaminaChanged(float current, float max)
    // {
    //     PlayerStamina01 = (max > 0f) ? current / max : 0f;
    //     ResetPlayerStamina();
    // }

    // private void ResetPlayerHp()
    // {
    //     if (HpBar != null)
    //     {
    //         HpBar.value = PlayerHp01;
    //     }
    // }

    // private void ResetPlayerStamina()
    // {
    //     if (StaminaBar != null)
    //     {
    //         StaminaBar.value = PlayerStamina01;
    //     }
    // }

    private void ResetEnemyHp()
    {
        if (EnemyHpBar != null)
        {
            float v = (Enemy.MaxHp > 0f) ? Enemy.CurrentHp / Enemy.MaxHp : 0f;
            EnemyHpBar.value = v;
        }
    }

    private void ResetEnemyStamina()
    {
        if (EnemyStaminaBars != null && EnemyStaminaBars.Length >= 2)
        {
            float v = (Enemy.MaxStamina > 0f) ? Enemy.CurrentStamina / Enemy.MaxStamina : 0f;
            EnemyStaminaBars[0].value = v;
            EnemyStaminaBars[1].value = v;
        }
    }
}
