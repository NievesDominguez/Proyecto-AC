using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

/// <summary>
/// Va en el jugador. Detecta el objeto recogible más cercano, muestra
/// "[E] Coger ..." y lo añade al inventario al pulsar E.
/// </summary>
public class ItemPickupInteractor : MonoBehaviour
{
    [Header("Referencias (se buscan solas si se dejan vacías)")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private DialogueRunner dialogueRunner;

    [Header("Entrada")]
    [Tooltip("Opcional. Si se deja vacío se usa la tecla E directamente.")]
    [SerializeField] private InputActionReference interactAction;

    [Header("Recogida")]
    [SerializeField] private float pickupRadius = 1.8f;

    private WorldItem current;

    private void Start()
    {
        if (inventory == null)
        {
            inventory = FindFirstObjectByType<Inventory>();
        }

        if (dialogueRunner == null)
        {
            dialogueRunner = FindFirstObjectByType<DialogueRunner>();
        }
    }

    private void OnDisable()
    {
        SetPrompt(null);
    }

    private void Update()
    {
        // No se recoge nada con el inventario abierto, colocando un objeto o durante un diálogo
        bool blocked =
            InventoryUI.IsOpen ||
            DecorationPlacer.IsPlacing ||
            (dialogueRunner != null && dialogueRunner.IsDialogueRunning);

        current = blocked ? null : WorldItem.GetNearest(transform.position, pickupRadius);

        SetPrompt(current);

        if (current == null || inventory == null)
        {
            return;
        }

        if (InteractPressed())
        {
            TryPickUp(current);
        }
    }

    private bool InteractPressed()
    {
        if (interactAction != null && interactAction.action != null)
        {
            return interactAction.action.WasPressedThisFrame();
        }

        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    private void TryPickUp(WorldItem target)
    {
        int added = inventory.Add(target.Item, target.Amount);

        if (added <= 0)
        {
            // Inventario lleno: el propio inventario ya avisa con un evento
            return;
        }

        if (added >= target.Amount)
        {
            Destroy(target.gameObject);
        }
        else
        {
            // Solo cupo una parte: el resto se queda en el suelo
            target.Amount -= added;
        }
    }

    private void SetPrompt(WorldItem target)
    {
        if (InteractionPrompt.Instance == null)
        {
            return;
        }

        InteractionPrompt.Instance.Set(
            this,
            target != null,
            target != null ? target.PromptText : null
        );
    }
}