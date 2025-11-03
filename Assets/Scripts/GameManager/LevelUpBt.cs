using UnityEngine;
using UnityEngine.InputSystem;

public class LevelUpBt : MonoBehaviour
{
    private InputAction levelUpAction;
    public LevelManage Lm;

    private void Awake()
    {
        levelUpAction = new InputAction(
            name: "LevelUp",
            type: InputActionType.Button,
            binding: "<Keyboard>/l"
        );
    }

    private void OnEnable()
    {
        levelUpAction.Enable();
        levelUpAction.started += OnLevelUpStarted;
    }

    private void OnDisable()
    {
        levelUpAction.started -= OnLevelUpStarted;
        levelUpAction.Disable();
    }

    private void OnLevelUpStarted(InputAction.CallbackContext context)
    {
        Lm.GetExp(100);
    }
}
