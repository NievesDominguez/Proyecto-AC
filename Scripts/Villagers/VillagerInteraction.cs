using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

public class VillagerInteraction : MonoBehaviour
{
    [Header("Yarn Spinner")]
    [SerializeField] private DialogueRunner dialogueRunner;
    [SerializeField] private VariableStorageBehaviour variableStorage;

    [Header("Datos del aldeano")]
    [SerializeField] private VillagerPersonality personality;
    [SerializeField] private VillagerConversationSelector conversationSelector;

    [Tooltip("Opcional: si se asigna, el aldeano se detiene y mira al jugador mientras habla.")]
    [SerializeField] private VillagerRoutine routine;

    [Header("Mundo")]
    [SerializeField] private WorldState worldState;

    [Header("Entrada")]
    [SerializeField] private InputActionReference interactAction;

    private bool playerInRange;
    private Transform playerTransform;

    // Control de la conversación para reanudar la rutina al terminar
    private bool isTalking;
    private bool sawDialogueRunning;

    private void OnEnable()
    {
        if (interactAction != null)
        {
            interactAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.action.Disable();
        }

        playerInRange = false;
        SetPrompt(false);

        if (isTalking)
        {
            EndTalking();
        }
    }

    private void Update()
    {
        // Cuando termina el diálogo, el aldeano retoma su rutina.
        // Esperamos a haber visto el diálogo en marcha para no cortarlo
        // antes de que arranque.
        if (isTalking && dialogueRunner != null)
        {
            if (dialogueRunner.IsDialogueRunning)
            {
                sawDialogueRunning = true;
            }
            else if (sawDialogueRunning)
            {
                EndTalking();
            }
        }

        // El indicador se muestra solo si el jugador está cerca y no hay un diálogo en marcha y no está abierto el inventario
        bool canTalk =
            playerInRange &&
            dialogueRunner != null &&
            !dialogueRunner.IsDialogueRunning &&
            !InventoryUI.IsOpen;

        SetPrompt(canTalk);

        if (!canTalk)
        {
            return;
        }

        if (conversationSelector == null || personality == null || variableStorage == null)
        {
            return;
        }

        if (interactAction == null || interactAction.action == null)
        {
            return;
        }

        if (interactAction.action.WasPressedThisFrame())
        {
            StartConversation();
        }
    }

    private void StartConversation()
    {
        // 1. Volcamos el estado a las variables de Yarn.
        personality.PushTo(variableStorage);

        if (worldState != null)
        {
            worldState.PushTo(variableStorage);
        }

        // 2. Elegimos el grupo y dejamos que Yarn escoja el nodo concreto.
        string groupName = conversationSelector.SelectConversation();

        if (string.IsNullOrEmpty(groupName))
        {
            return;
        }

        // 3. El aldeano se para y mira al jugador.
        if (routine != null)
        {
            routine.BeginConversation(playerTransform);
        }

        isTalking = true;
        sawDialogueRunning = false;

        dialogueRunner.StartDialogue(groupName);
    }

    private void EndTalking()
    {
        isTalking = false;
        sawDialogueRunning = false;

        if (routine != null)
        {
            routine.EndConversation();
        }
    }

    private void SetPrompt(bool visible)
    {
        if (InteractionPrompt.Instance != null)
        {
            InteractionPrompt.Instance.Set(this, visible);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            playerTransform = other.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }
}