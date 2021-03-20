using UnityEngine;

public class CustomCharacterController : MonoBehaviour
{
    private InputActions m_inputActions;
    private CustomCharacterMotor m_characterMotor;

    void Awake()
    {
        m_inputActions = new InputActions();
        m_inputActions.Enable();
        m_characterMotor = GetComponent<CustomCharacterMotor>();
    }

    void FixedUpdate()
    {
        ReadInput(out Vector2 movement, out float rotationDelta);
        m_characterMotor.Rotate(rotationDelta);
        m_characterMotor.Move(movement);
    }

    private void ReadInput(out Vector2 movement, out float rotation)
    {
        movement = m_inputActions.Player.Movement.ReadValue<Vector2>();
        rotation = m_inputActions.Player.HorizontalRotation.ReadValue<float>();
    }
}
