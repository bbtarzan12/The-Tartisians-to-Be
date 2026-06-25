using UnityEngine;
using UnityEngine.InputSystem;

namespace Tartisians.Gameplay.Input
{
    /// <summary>이동 입력을 노출하는 추상. 테스트에서 스텁으로 대체 가능하다.</summary>
    public interface IMoveInputSource
    {
        Vector2 MoveInput { get; }
    }

    /// <summary>
    /// New Input System 기반 입력 리더. 레거시 UnityEngine.Input은 사용하지 않는다.
    /// 코드로 액션을 구성해 코드젠 의존 없이 결정론적으로 동작한다(WASD·방향키·게임패드).
    /// </summary>
    public sealed class InputReader : MonoBehaviour, IMoveInputSource
    {
        InputAction _moveAction;

        public Vector2 MoveInput { get; private set; }

        void OnEnable()
        {
            _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");

            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            _moveAction.AddBinding("<Gamepad>/leftStick");

            _moveAction.Enable();
        }

        void Update()
        {
            MoveInput = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
        }

        void OnDisable()
        {
            _moveAction?.Disable();
            _moveAction?.Dispose();
            _moveAction = null;
            MoveInput = Vector2.zero;
        }
    }
}
