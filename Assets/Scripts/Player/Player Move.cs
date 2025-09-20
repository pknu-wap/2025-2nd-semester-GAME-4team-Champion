using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public partial class PlayerMove : IInputActionCollection2, IDisposable
{
    public InputActionAsset asset { get; }

    public PlayerMove()
    {
        asset = InputActionAsset.FromJson(@"{
            ""name"": ""Player Move"",
            ""maps"": [
                {
                    ""name"": ""Movement"",
                    ""id"": ""68b000a2-0d58-40b4-ab88-f63e38a5b5ea"",
                    ""actions"": [
                        {
                            ""name"": ""Move"",
                            ""type"": ""PassThrough"",
                            ""id"": ""f2b87111-8188-4b3e-aad4-d9f265b9d779"",
                            ""expectedControlType"": ""Vector2""
                        }
                    ],
                    ""bindings"": [
                        {""name"": ""2D Vector"", ""id"": ""46e10468-e815-4035-85af-d8d5d1502166"", ""path"": ""2DVector"", ""action"": ""Move"", ""isComposite"": true},
                        {""name"": ""up"",    ""path"": ""<Keyboard>/w"", ""action"": ""Move"", ""isPartOfComposite"": true},
                        {""name"": ""down"",  ""path"": ""<Keyboard>/s"", ""action"": ""Move"", ""isPartOfComposite"": true},
                        {""name"": ""left"",  ""path"": ""<Keyboard>/a"", ""action"": ""Move"", ""isPartOfComposite"": true},
                        {""name"": ""right"", ""path"": ""<Keyboard>/d"", ""action"": ""Move"", ""isPartOfComposite"": true}
                    ]
                }
            ]
        }");

        m_Movement = asset.FindActionMap("Movement", throwIfNotFound: true);
        m_Movement_Move = m_Movement.FindAction("Move", throwIfNotFound: true);
    }

    ~PlayerMove()
    {
        UnityEngine.Debug.Assert(!m_Movement.enabled, "Movement.Disable()가 호출되지 않음");
    }

    public void Dispose() => UnityEngine.Object.Destroy(asset);

    public InputBinding? bindingMask { get => asset.bindingMask; set => asset.bindingMask = value; }
    public ReadOnlyArray<InputDevice>? devices { get => asset.devices; set => asset.devices = value; }
    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

    public bool Contains(InputAction action) => asset.Contains(action);
    public IEnumerator<InputAction> GetEnumerator() => asset.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Enable() => asset.Enable();
    public void Disable() => asset.Disable();
    public IEnumerable<InputBinding> bindings => asset.bindings;

    // ✅ 빠진 인터페이스 멤버들 구현
    public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false)
        => asset.FindAction(actionNameOrId, throwIfNotFound);

    public int FindBinding(InputBinding bindingMask, out InputAction action)
        => asset.FindBinding(bindingMask, out action);

    // Movement
    private readonly InputActionMap m_Movement;
    private readonly InputAction m_Movement_Move;
    private List<IMovementActions> m_MovementActionsCallbackInterfaces = new();

    public struct MovementActions
    {
        private PlayerMove m_Wrapper;
        public MovementActions(PlayerMove wrapper) { m_Wrapper = wrapper; }

        public InputAction Move => m_Wrapper.m_Movement_Move;
        public InputActionMap Get() => m_Wrapper.m_Movement;
        public void Enable() => Get().Enable();
        public void Disable() => Get().Disable();
        public bool enabled => Get().enabled;
        public static implicit operator InputActionMap(MovementActions set) => set.Get();

        public void AddCallbacks(IMovementActions instance)
        {
            if (instance == null || m_Wrapper.m_MovementActionsCallbackInterfaces.Contains(instance)) return;
            m_Wrapper.m_MovementActionsCallbackInterfaces.Add(instance);
            Move.started += instance.OnMove;
            Move.performed += instance.OnMove;
            Move.canceled += instance.OnMove;
        }

        private void UnregisterCallbacks(IMovementActions instance)
        {
            Move.started -= instance.OnMove;
            Move.performed -= instance.OnMove;
            Move.canceled -= instance.OnMove;
        }

        public void RemoveCallbacks(IMovementActions instance)
        {
            if (m_Wrapper.m_MovementActionsCallbackInterfaces.Remove(instance))
                UnregisterCallbacks(instance);
        }

        public void SetCallbacks(IMovementActions instance)
        {
            foreach (var item in m_Wrapper.m_MovementActionsCallbackInterfaces)
                UnregisterCallbacks(item);
            m_Wrapper.m_MovementActionsCallbackInterfaces.Clear();
            AddCallbacks(instance);
        }
    }

    public MovementActions Movement => new MovementActions(this);

    public interface IMovementActions
    {
        void OnMove(InputAction.CallbackContext context);
    }
}
