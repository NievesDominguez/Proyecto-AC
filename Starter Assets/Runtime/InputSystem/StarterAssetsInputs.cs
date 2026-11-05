using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

		[Header("Input Lock")]
		public bool inputLocked;

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			if (inputLocked) return;
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if (inputLocked) return;
			if (cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			if (inputLocked) return;
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			if (inputLocked) return;
			SprintInput(value.isPressed);
		}
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		}

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}

		/// <summary>
		/// Bloquea o desbloquea la entrada del personaje y de la cámara
		/// (por ejemplo, durante un diálogo).
		/// </summary>
		public void SetLocked(bool locked)
		{
			inputLocked = locked;

			if (locked)
			{
				move = Vector2.zero;
				look = Vector2.zero;
				jump = false;
				sprint = false;
				return;
			}

#if ENABLE_INPUT_SYSTEM
			// Si el jugador mantiene una tecla al terminar el diálogo,
			// recuperamos su estado real (si no, no se movería hasta
			// soltar y volver a pulsar).
			var playerInput = GetComponent<PlayerInput>();
			if (playerInput == null) return;

			var moveAction = playerInput.actions.FindAction("Move");
			var sprintAction = playerInput.actions.FindAction("Sprint");

			if (moveAction != null) move = moveAction.ReadValue<Vector2>();
			if (sprintAction != null) sprint = sprintAction.IsPressed();
#endif
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
	}

}