using System.Collections;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Yarn.Unity;

/// <summary>
/// Interfaz del inventario. Se abre con I y se cierra con I o Escape.
/// Se construye entera por código: basta con añadir este componente
/// (junto a Inventory) a un objeto de la escena.
///
/// Mover objetos (al estilo Minecraft; lo que se mueve queda "en la mano"):
///   - Clic izquierdo / E / Enter: coger la pila entera o, con algo en la mano, colocarla toda
///   - Clic derecho: coger la mitad de la pila o, con algo en la mano, colocar solo una unidad
///   - Mayús + E / Enter / clic: lo mismo que el clic derecho
///   - Arrastrar con el botón izquierdo: mover la pila entera a otra casilla
///   - Escape: devolver lo que llevas en la mano
///
/// Con un objeto seleccionado:
///   - F o el botón "Colocar": lo coloca en el mundo (si es un objeto decorativo)
///   - Q o el botón "Soltar": suelta una unidad al suelo
/// </summary>
public class InventoryUI : MonoBehaviour, IInventorySlotHandler
{
    private const string FooterNormal =
        "[E] / clic: coger o colocar     Clic derecho: coger la mitad / colocar uno     Arrastrar: mover\n" +
        "[Mayús]+[E]: como clic derecho     [F] Colocar en el mundo     [Q] Soltar     [I] Cerrar";

    private const string FooterCarrying =
        "[E] / clic: colocar todo     Clic derecho: colocar uno\n" +
        "[Mayús]+[E]: colocar uno     [Esc] Devolver";

    /// <summary>Otros sistemas (recoger, hablar...) consultan esto para no interferir.</summary>
    public static bool IsOpen { get; private set; }

    [Header("Referencias (se buscan solas si se dejan vacías)")]
    [SerializeField] private Inventory inventory;
    [SerializeField] private StarterAssetsInputs playerInputs;
    [SerializeField] private DialogueRunner dialogueRunner;

    [Header("Distribución")]
    [SerializeField, Min(1)] private int columns = 5;
    [SerializeField] private float slotSize = 100f;
    [SerializeField] private float slotSpacing = 14f;

    [Header("Colores")]
    [SerializeField] private Color panelColor = new Color(0.05f, 0.09f, 0.12f, 0.95f);
    [SerializeField] private Color accentColor = new Color(0.38f, 0.88f, 0.82f, 1f);
    [SerializeField] private Color slotColor = new Color(1f, 1f, 1f, 0.07f);
    [SerializeField] private Color slotSelectedColor = new Color(0.38f, 0.88f, 0.82f, 0.30f);

    // Elementos de la interfaz
    private CanvasGroup windowGroup;
    private RectTransform windowRect;
    private TextMeshProUGUI capacityText;
    private TextMeshProUGUI nameText;
    private TextMeshProUGUI amountText;
    private TextMeshProUGUI descriptionText;
    private TextMeshProUGUI emptyText;
    private TextMeshProUGUI footerText;
    private Image previewImage;
    private GameObject detailContent;
    private GameObject dropButtonObject;
    private GameObject placeButtonObject;
    private RectTransform dropButtonRect;
    private RectTransform placeButtonRect;
    private float detailWidth;
    private float detailHeight;
    private CanvasGroup toastGroup;
    private TextMeshProUGUI toastText;
    private InventorySlotView[] views;

    // Objeto "en la mano" mientras se mueve
    private RectTransform ghostRect;
    private Image ghostIcon;
    private TextMeshProUGUI ghostAmount;

    // Estado
    private int selectedIndex;
    private ItemData handItem;
    private int handAmount;
    private int handOrigin = -1;   // casilla de la que salió (para devolverlo ahí)
    private bool carryFromDrag;
    private float lastMouseMoveTime = -10f;
    private Transform playerTransform;
    private Coroutine windowRoutine;
    private Coroutine toastRoutine;

    private bool IsCarrying => handItem != null && handAmount > 0;

    // ------------------------------------------------------------------
    // CICLO DE VIDA
    // ------------------------------------------------------------------
    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<Inventory>();
        if (inventory == null) inventory = FindFirstObjectByType<Inventory>();
        if (playerInputs == null) playerInputs = FindFirstObjectByType<StarterAssetsInputs>();
        if (dialogueRunner == null) dialogueRunner = FindFirstObjectByType<DialogueRunner>();

        if (inventory == null)
        {
            Debug.LogError("InventoryUI: no se encontró ningún componente Inventory en la escena.", this);
            enabled = false;
            return;
        }

        if (playerInputs != null)
        {
            playerTransform = playerInputs.transform;
        }

        BuildUI();
    }

    private void OnEnable()
    {
        IsOpen = false;

        if (inventory == null)
        {
            return;
        }

        inventory.Changed += Refresh;
        inventory.ItemAdded += HandleItemAdded;
        inventory.InventoryFull += HandleInventoryFull;
    }

    private void OnDisable()
    {
        if (IsOpen)
        {
            CloseImmediate();
        }

        if (inventory == null)
        {
            return;
        }

        inventory.Changed -= Refresh;
        inventory.ItemAdded -= HandleItemAdded;
        inventory.InventoryFull -= HandleInventoryFull;
    }

    private void Start()
    {
        Refresh();

        if (EventSystem.current == null)
        {
            Debug.LogWarning(
                "InventoryUI: no hay ningún EventSystem en la escena; la navegación por casillas no funcionará.",
                this
            );
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.iKey.wasPressedThisFrame)
            {
                if (IsOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
            }
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                // Con algo en la mano, Escape solo lo devuelve; no cierra la bolsa
                if (IsCarrying)
                {
                    ReturnHand();
                }
                else
                {
                    Close();
                }
            }

            if (IsOpen && !IsCarrying)
            {
                if (keyboard.qKey.wasPressedThisFrame)
                {
                    DropSelected();
                }
                else if (keyboard.fKey.wasPressedThisFrame)
                {
                    PlaceSelected();
                }
            }
        }

        if (IsOpen && IsCarrying)
        {
            UpdateGhostPosition(false);
        }
    }

    // ------------------------------------------------------------------
    // ABRIR Y CERRAR
    // ------------------------------------------------------------------
    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        // No se abre durante un diálogo
        if (dialogueRunner != null && dialogueRunner.IsDialogueRunning)
        {
            return;
        }

        // Si se estaba colocando un objeto, se cancela para abrir la bolsa
        if (DecorationPlacer.IsPlacing && DecorationPlacer.Instance != null)
        {
            DecorationPlacer.Instance.Cancel();
        }

        IsOpen = true;
        ReturnHand();

        Refresh();

        if (playerInputs != null)
        {
            playerInputs.SetLocked(true);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        windowGroup.interactable = true;
        windowGroup.blocksRaycasts = true;
        StartWindowAnimation(true);

        // Selecciona una casilla para poder navegar con teclado o mando
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(views[Mathf.Clamp(selectedIndex, 0, views.Length - 1)].gameObject);
        }
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        CloseImmediate();
        StartWindowAnimation(false);
    }

    // Parte común de cerrar (también se usa si el componente se desactiva)
    private void CloseImmediate()
    {
        // Lo que llevabas en la mano vuelve a la bolsa
        ReturnHand();

        IsOpen = false;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (playerInputs != null)
        {
            playerInputs.SetLocked(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (windowGroup != null)
        {
            windowGroup.interactable = false;
            windowGroup.blocksRaycasts = false;
        }
    }

    private void StartWindowAnimation(bool opening)
    {
        if (windowRoutine != null)
        {
            StopCoroutine(windowRoutine);
        }

        windowRoutine = StartCoroutine(AnimateWindow(opening));
    }

    private IEnumerator AnimateWindow(bool opening)
    {
        const float duration = 0.18f;

        float startAlpha = windowGroup.alpha;
        float targetAlpha = opening ? 1f : 0f;
        Vector2 startPosition = windowRect.anchoredPosition;
        Vector2 targetPosition = opening ? Vector2.zero : new Vector2(0f, -30f);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            windowGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, k);
            windowRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, k);

            yield return null;
        }

        windowGroup.alpha = targetAlpha;
        windowRect.anchoredPosition = targetPosition;
    }

    // ------------------------------------------------------------------
    // CONTENIDO
    // ------------------------------------------------------------------
    private void Refresh()
    {
        if (views == null || inventory == null)
        {
            return;
        }

        for (int i = 0; i < views.Length; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);

            if (slot == null || slot.IsEmpty)
            {
                views[i].Clear();
                continue;
            }

            Sprite sprite = GetIcon(slot.item, out Color tint);
            views[i].SetContent(sprite, tint, slot.amount);
        }

        capacityText.text = $"{inventory.UsedSlots} / {inventory.Capacity}";

        UpdateHighlights();
        UpdateDetails();
    }

    private void UpdateHighlights()
    {
        for (int i = 0; i < views.Length; i++)
        {
            views[i].SetHighlighted(i == selectedIndex);
        }
    }

    private void UpdateDetails()
    {
        ItemData item;
        int amount;

        // Con algo en la mano, la tarjeta enseña eso; si no, la casilla seleccionada
        if (IsCarrying)
        {
            item = handItem;
            amount = handAmount;
        }
        else
        {
            InventorySlot slot = inventory.GetSlot(selectedIndex);
            item = slot != null && !slot.IsEmpty ? slot.item : null;
            amount = slot != null ? slot.amount : 0;
        }

        bool hasItem = item != null;

        detailContent.SetActive(hasItem);
        emptyText.gameObject.SetActive(!hasItem);

        if (!hasItem)
        {
            return;
        }

        previewImage.sprite = GetIcon(item, out Color tint);
        previewImage.color = tint;

        nameText.text = item.displayName;
        amountText.text = item.maxStack > 1
            ? $"x{amount}   ·   máx. {item.maxStack}"
            : "Objeto único";
        descriptionText.text = item.description;

        // Los botones no tienen sentido mientras se mueve un objeto
        UpdateButtons(!IsCarrying, !IsCarrying && item.placeable);
    }

    // "Colocar" solo aparece en objetos decorativos; "Soltar" se centra si está solo
    private void UpdateButtons(bool showButtons, bool showPlace)
    {
        dropButtonObject.SetActive(showButtons);
        placeButtonObject.SetActive(showPlace);

        float y = detailHeight - 76f;

        if (showPlace)
        {
            Place(placeButtonRect, 12f, y, 190f, 56f);
            Place(dropButtonRect, 218f, y, 190f, 56f);
        }
        else
        {
            Place(dropButtonRect, (detailWidth - 240f) / 2f, y, 240f, 56f);
        }
    }

    private void UpdateFooter()
    {
        footerText.text = IsCarrying ? FooterCarrying : FooterNormal;
    }

    // Devuelve el icono del objeto o, si no tiene, uno provisional teñido con su color
    private Sprite GetIcon(ItemData item, out Color tint)
    {
        if (item.icon != null)
        {
            tint = Color.white;
            return item.icon;
        }

        tint = item.placeholderColor;
        return item.placeholderShape == PlaceholderShape.Sphere ? UISprites.Sphere() : UISprites.Cube();
    }

    // ------------------------------------------------------------------
    // EVENTOS DE LAS CASILLAS (IInventorySlotHandler)
    // ------------------------------------------------------------------
    public void OnSlotSelected(int index)
    {
        selectedIndex = index;
        UpdateHighlights();
        UpdateDetails();
    }

    // Clic izquierdo o E/Enter. Con Mayús pulsado actúa como el clic derecho.
    public void OnSlotActivated(int index)
    {
        if (!IsOpen)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool secondary = keyboard != null && keyboard.shiftKey.isPressed;

        if (secondary)
        {
            HandleSecondary(index);
        }
        else
        {
            HandlePrimary(index);
        }
    }

    public void OnSlotRightClicked(int index)
    {
        if (!IsOpen)
        {
            return;
        }

        HandleSecondary(index);
    }

    public void OnSlotBeginDrag(int index)
    {
        if (!IsOpen || IsCarrying)
        {
            return;
        }

        // Arrastrar siempre mueve la pila entera
        PickUp(index, false, true);
    }

    public void OnSlotEndDrag(int index, PointerEventData eventData)
    {
        // Solo nos interesa si este arrastre empezó cogiendo un objeto
        if (!carryFromDrag)
        {
            return;
        }

        InventorySlotView target = null;
        GameObject hit = eventData.pointerCurrentRaycast.gameObject;

        if (hit != null)
        {
            target = hit.GetComponentInParent<InventorySlotView>();
        }

        if (target == null)
        {
            // Soltado fuera de las casillas: vuelve a su sitio
            ReturnHand();
            return;
        }

        PlaceAll(target.Index);

        // Si algo se quedó en la mano (resto o intercambio), se sigue con clics
        carryFromDrag = false;
    }

    // ------------------------------------------------------------------
    // MOVER OBJETOS: LA "MANO"
    // ------------------------------------------------------------------
    private void HandlePrimary(int index)
    {
        if (!IsCarrying)
        {
            PickUp(index, false, false);
        }
        else
        {
            PlaceAll(index);
        }
    }

    private void HandleSecondary(int index)
    {
        if (!IsCarrying)
        {
            PickUp(index, true, false);
        }
        else
        {
            PlaceOne(index);
        }
    }

    // Saca de una casilla la pila entera o la mitad (redondeando hacia arriba, como en Minecraft)
    private void PickUp(int index, bool half, bool fromDrag)
    {
        InventorySlot slot = inventory.GetSlot(index);

        if (slot == null || slot.IsEmpty)
        {
            return;
        }

        int amount = half ? Mathf.CeilToInt(slot.amount / 2f) : slot.amount;
        int taken = inventory.Take(index, amount, out ItemData item);

        if (taken <= 0)
        {
            return;
        }

        SetHand(item, taken, index);
        carryFromDrag = fromDrag;
    }

    // Coloca todo lo que hay en la mano. Si no cabe todo, el resto se queda en la mano.
    private void PlaceAll(int index)
    {
        if (!IsCarrying)
        {
            return;
        }

        InventorySlot slot = inventory.GetSlot(index);

        if (slot == null)
        {
            return;
        }

        if (slot.IsEmpty || slot.item == handItem)
        {
            int placed = inventory.PlaceInto(index, handItem, handAmount);

            if (placed <= 0)
            {
                ShowToast("Esa pila ya está completa");
                return;
            }

            SetHand(handItem, handAmount - placed, -1);
        }
        else
        {
            SwapHand(index);
        }

        AfterPlace(index);
    }

    // Coloca una sola unidad de las que hay en la mano
    private void PlaceOne(int index)
    {
        if (!IsCarrying)
        {
            return;
        }

        InventorySlot slot = inventory.GetSlot(index);

        if (slot == null)
        {
            return;
        }

        if (slot.IsEmpty || slot.item == handItem)
        {
            int placed = inventory.PlaceInto(index, handItem, 1);

            if (placed <= 0)
            {
                ShowToast("Esa pila ya está completa");
                return;
            }

            SetHand(handItem, handAmount - 1, -1);
        }
        else
        {
            SwapHand(index);
        }

        AfterPlace(index);
    }

    // Cambia lo que llevas en la mano por lo que hay en la casilla
    private void SwapHand(int index)
    {
        if (inventory.SwapWith(index, handItem, handAmount, out ItemData oldItem, out int oldAmount))
        {
            // Lo que cogemos ahora "viene" de esta casilla
            SetHand(oldItem, oldAmount, index);
        }
    }

    // La selección se queda en la casilla donde se acaba de colocar
    private void AfterPlace(int index)
    {
        selectedIndex = index;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(views[index].gameObject);
        }

        UpdateHighlights();
        UpdateDetails();
    }

    // Actualiza lo que hay en la mano (cantidad 0 = mano vacía)
    private void SetHand(ItemData item, int amount, int origin)
    {
        if (amount <= 0 || item == null)
        {
            handItem = null;
            handAmount = 0;
            handOrigin = -1;
            carryFromDrag = false;

            if (ghostRect != null)
            {
                ghostRect.gameObject.SetActive(false);
            }
        }
        else
        {
            bool wasEmpty = !IsCarrying;

            handItem = item;
            handAmount = amount;

            if (origin >= 0)
            {
                handOrigin = origin;
            }

            ghostIcon.sprite = GetIcon(item, out Color tint);
            ghostIcon.color = tint;
            ghostAmount.text = amount > 1 ? $"x{amount}" : string.Empty;

            ghostRect.gameObject.SetActive(true);
            UpdateGhostPosition(wasEmpty);
        }

        UpdateDetails();
        UpdateFooter();
    }

    // Devuelve lo que hay en la mano: a su casilla de origen si puede, si no a cualquier
    // hueco y, como último recurso, al suelo.
    private void ReturnHand()
    {
        if (!IsCarrying)
        {
            SetHand(null, 0, -1);
            return;
        }

        ItemData item = handItem;
        int remaining = handAmount;

        if (handOrigin >= 0)
        {
            remaining -= inventory.PlaceInto(handOrigin, item, remaining);
        }

        if (remaining > 0)
        {
            remaining -= inventory.Add(item, remaining, true);
        }

        SetHand(null, 0, -1);

        if (remaining > 0)
        {
            WorldItem.Spawn(item, remaining, GetDropPosition());
            ShowToast("Bolsa llena: el objeto cayó al suelo");
        }
    }

    // Coloca el objeto en la mano: sigue al ratón o, con teclado, a la casilla seleccionada
    private void UpdateGhostPosition(bool instant)
    {
        if (ghostRect == null || !IsCarrying)
        {
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0.5f)
        {
            lastMouseMoveTime = Time.unscaledTime;
        }

        bool followMouse = mouse != null &&
            (carryFromDrag || Time.unscaledTime - lastMouseMoveTime < 2f);

        if (followMouse)
        {
            // En un Canvas "Overlay", la posición del elemento equivale a píxeles de pantalla
            ghostRect.position = mouse.position.ReadValue();
            return;
        }

        RectTransform slotRect = (RectTransform)views[Mathf.Clamp(selectedIndex, 0, views.Length - 1)].transform;
        Vector3 target = slotRect.TransformPoint(slotRect.rect.center) + new Vector3(0f, 10f, 0f);

        if (instant)
        {
            ghostRect.position = target;
        }
        else
        {
            float blend = 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime);
            ghostRect.position = Vector3.Lerp(ghostRect.position, target, blend);
        }
    }

    // ------------------------------------------------------------------
    // COLOCAR EN EL MUNDO (DECORACIÓN)
    // ------------------------------------------------------------------
    private void PlaceSelected()
    {
        if (IsCarrying)
        {
            return;
        }

        InventorySlot slot = inventory.GetSlot(selectedIndex);

        if (slot == null || slot.IsEmpty || !slot.item.placeable)
        {
            return;
        }

        DecorationPlacer placer = DecorationPlacer.Instance;

        if (placer == null)
        {
            ShowToast("Falta el sistema de decoración en la escena");
            return;
        }

        ItemData item = slot.item;
        int index = selectedIndex;

        // Se cierra la bolsa para poder moverse y elegir el sitio
        Close();
        placer.BeginPlacement(item, index);
    }

    // ------------------------------------------------------------------
    // SOLTAR AL SUELO
    // ------------------------------------------------------------------
    private void DropSelected()
    {
        if (IsCarrying)
        {
            return;
        }

        InventorySlot slot = inventory.GetSlot(selectedIndex);

        if (slot == null || slot.IsEmpty)
        {
            return;
        }

        ItemData item = slot.item;

        if (!inventory.RemoveAt(selectedIndex, 1))
        {
            return;
        }

        WorldItem.Spawn(item, 1, GetDropPosition());
        ShowToast($"Soltaste {item.displayName}");

        // Si se pulsó el botón con el ratón, devolvemos la selección a la casilla
        // para que el teclado siga funcionando
        if (IsOpen && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(views[selectedIndex].gameObject);
        }
    }

    private Vector3 GetDropPosition()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        if (playerTransform == null)
        {
            return Vector3.up;
        }

        Vector3 ahead = playerTransform.position + playerTransform.forward * 1.3f;

        // Buscamos el suelo desde arriba para que el objeto no quede flotando ni enterrado
        if (Physics.Raycast(ahead + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.35f;
        }

        return ahead + Vector3.up * 0.5f;
    }

    // ------------------------------------------------------------------
    // AVISOS ("+1 Esfera roja", "Bolsa llena"...)
    // ------------------------------------------------------------------
    private void HandleItemAdded(ItemData item, int amount)
    {
        ShowToast($"+{amount}   {item.displayName}");
    }

    private void HandleInventoryFull(ItemData item)
    {
        ShowToast("Bolsa llena");
    }

    private void ShowToast(string message)
    {
        toastText.text = message;

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
        }

        toastRoutine = StartCoroutine(ToastRoutine());
    }

    private IEnumerator ToastRoutine()
    {
        yield return FadeToast(1f, 0.15f);

        float wait = 0f;

        while (wait < 1.6f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return FadeToast(0f, 0.4f);
    }

    private IEnumerator FadeToast(float target, float duration)
    {
        float start = toastGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            toastGroup.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }

        toastGroup.alpha = target;
    }

    // ------------------------------------------------------------------
    // CONSTRUCCIÓN DE LA INTERFAZ
    // ------------------------------------------------------------------
    private void BuildUI()
    {
        Sprite rounded = UISprites.RoundedRect();

        int rows = Mathf.CeilToInt(inventory.Capacity / (float)columns);
        float gridWidth = columns * slotSize + (columns - 1) * slotSpacing;
        float gridHeight = rows * slotSize + (rows - 1) * slotSpacing;

        const float padding = 40f;
        const float headerHeight = 90f;
        const float footerHeight = 84f;
        const float gap = 36f;
        detailWidth = 420f;

        float contentHeight = Mathf.Max(gridHeight, 520f);
        detailHeight = contentHeight;

        float windowWidth = padding * 2f + gridWidth + gap + detailWidth;
        float windowHeight = padding * 2f + headerHeight + contentHeight + footerHeight;

        // --- Canvas ---
        GameObject canvasObject = new GameObject(
            "InventoryCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // --- Ventana ---
        GameObject windowObject = new GameObject("Window", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        windowObject.transform.SetParent(canvasObject.transform, false);

        windowRect = windowObject.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(windowWidth, windowHeight);
        windowRect.anchoredPosition = new Vector2(0f, -30f);

        windowGroup = windowObject.GetComponent<CanvasGroup>();
        windowGroup.alpha = 0f;
        windowGroup.interactable = false;
        windowGroup.blocksRaycasts = false;

        Image windowBackground = windowObject.GetComponent<Image>();
        windowBackground.sprite = rounded;
        windowBackground.type = Image.Type.Sliced;
        windowBackground.color = panelColor;

        // --- Cabecera ---
        TextMeshProUGUI title = CreateText("Title", windowRect, "BOLSA", 46f, accentColor,
            TextAlignmentOptions.Left, FontStyles.Bold);
        Place(title.rectTransform, padding, padding - 6f, 400f, 60f);

        capacityText = CreateText("Capacity", windowRect, "0 / 0", 30f, new Color(1f, 1f, 1f, 0.8f),
            TextAlignmentOptions.Right);
        Place(capacityText.rectTransform, windowWidth - padding - 300f, padding + 4f, 300f, 50f);

        Image line = CreateImage("AccentLine", windowRect, null,
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.45f));
        Place(line.rectTransform, padding, padding + headerHeight - 14f, windowWidth - padding * 2f, 3f);

        // --- Cuadrícula de casillas ---
        RectTransform gridRect = CreateRect("Grid", windowRect);
        Place(gridRect, padding, padding + headerHeight, gridWidth, gridHeight);

        GridLayoutGroup grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(slotSize, slotSize);
        grid.spacing = new Vector2(slotSpacing, slotSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperLeft;

        views = new InventorySlotView[inventory.Capacity];

        for (int i = 0; i < views.Length; i++)
        {
            views[i] = CreateSlot(i, gridRect, rounded);
        }

        // --- Tarjeta de detalle ---
        Image card = CreateImage("DetailCard", windowRect, rounded, new Color(1f, 1f, 1f, 0.06f));
        card.type = Image.Type.Sliced;
        Place(card.rectTransform, padding + gridWidth + gap, padding + headerHeight, detailWidth, contentHeight);

        emptyText = CreateText("EmptyText", card.rectTransform, "Casilla vacía", 28f,
            new Color(1f, 1f, 1f, 0.35f), TextAlignmentOptions.Center);
        Stretch(emptyText.rectTransform, 20f, 20f, 20f, 20f);

        RectTransform content = CreateRect("Content", card.rectTransform);
        Stretch(content, 0f, 0f, 0f, 0f);
        detailContent = content.gameObject;

        previewImage = CreateImage("Preview", content, null, Color.white);
        previewImage.preserveAspect = true;
        Place(previewImage.rectTransform, (detailWidth - 160f) / 2f, 28f, 160f, 160f);

        nameText = CreateText("Name", content, "", 38f, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Place(nameText.rectTransform, 20f, 204f, detailWidth - 40f, 50f);

        amountText = CreateText("Amount", content, "", 24f, accentColor, TextAlignmentOptions.Center);
        Place(amountText.rectTransform, 20f, 256f, detailWidth - 40f, 34f);

        descriptionText = CreateText("Description", content, "", 24f, new Color(1f, 1f, 1f, 0.7f),
            TextAlignmentOptions.TopLeft);
        Place(descriptionText.rectTransform, 30f, 304f, detailWidth - 60f, contentHeight - 304f - 90f);

        // Botones: "Colocar" (solo en objetos decorativos) y "Soltar"
        placeButtonObject = BuildActionButton(content, rounded, "Colocar   [F]", PlaceSelected, out placeButtonRect);
        dropButtonObject = BuildActionButton(content, rounded, "Soltar   [Q]", DropSelected, out dropButtonRect);

        // --- Pie (dos líneas) ---
        footerText = CreateText("Footer", windowRect, FooterNormal, 21f,
            new Color(1f, 1f, 1f, 0.45f), TextAlignmentOptions.TopLeft);
        Place(footerText.rectTransform, padding, windowHeight - padding - 60f, windowWidth - padding * 2f, 60f);

        // --- Aviso emergente ---
        GameObject toastObject = new GameObject("Toast", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        toastObject.transform.SetParent(canvasObject.transform, false);

        RectTransform toastRect = toastObject.GetComponent<RectTransform>();
        toastRect.anchorMin = new Vector2(0.5f, 0f);
        toastRect.anchorMax = new Vector2(0.5f, 0f);
        toastRect.pivot = new Vector2(0.5f, 0f);
        toastRect.anchoredPosition = new Vector2(0f, 90f);
        toastRect.sizeDelta = new Vector2(520f, 64f);

        Image toastBackground = toastObject.GetComponent<Image>();
        toastBackground.sprite = rounded;
        toastBackground.type = Image.Type.Sliced;
        toastBackground.color = new Color(0.05f, 0.09f, 0.12f, 0.92f);
        toastBackground.raycastTarget = false;

        toastGroup = toastObject.GetComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.interactable = false;
        toastGroup.blocksRaycasts = false;

        toastText = CreateText("Text", toastRect, "", 28f, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(toastText.rectTransform, 16f, 8f, 16f, 8f);

        // --- Objeto en la mano (siempre encima de todo lo demás) ---
        GameObject ghostObject = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup));
        ghostObject.transform.SetParent(canvasObject.transform, false);

        ghostRect = ghostObject.GetComponent<RectTransform>();
        ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
        ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
        ghostRect.pivot = new Vector2(0.5f, 0.5f);
        ghostRect.sizeDelta = new Vector2(slotSize * 0.9f, slotSize * 0.9f);
        ghostRect.localScale = Vector3.one * 1.1f;

        // No debe interceptar el ratón, o no se podría detectar la casilla de debajo
        CanvasGroup ghostGroup = ghostObject.GetComponent<CanvasGroup>();
        ghostGroup.alpha = 0.95f;
        ghostGroup.interactable = false;
        ghostGroup.blocksRaycasts = false;

        ghostIcon = CreateImage("Icon", ghostRect, null, Color.white);
        ghostIcon.preserveAspect = true;
        Stretch(ghostIcon.rectTransform, 4f, 4f, 4f, 4f);

        ghostAmount = CreateText("Amount", ghostRect, "", 26f, Color.white,
            TextAlignmentOptions.BottomRight, FontStyles.Bold);
        Stretch(ghostAmount.rectTransform, 4f, 4f, 8f, 4f);

        ghostObject.SetActive(false);
    }

    private InventorySlotView CreateSlot(int index, Transform parent, Sprite rounded)
    {
        GameObject slotObject = new GameObject($"Slot_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        slotObject.transform.SetParent(parent, false);

        Image background = slotObject.GetComponent<Image>();
        background.sprite = rounded;
        background.type = Image.Type.Sliced;
        background.color = slotColor;

        // Los colores y el tamaño los gestiona InventorySlotView, no el Button
        Button button = slotObject.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        Image icon = CreateImage("Icon", slotObject.transform, null, Color.white);
        icon.preserveAspect = true;
        Stretch(icon.rectTransform, 16f, 16f, 16f, 16f);

        TextMeshProUGUI amount = CreateText("Amount", slotObject.transform, "", 22f, Color.white,
            TextAlignmentOptions.BottomRight, FontStyles.Bold);
        Stretch(amount.rectTransform, 6f, 4f, 10f, 4f);

        InventorySlotView view = slotObject.AddComponent<InventorySlotView>();
        view.Setup(index, background, icon, amount, slotColor, slotSelectedColor, this);

        return view;
    }

    // Botón de acción de la tarjeta de detalle (solo se usa con el ratón; con teclado hay atajos)
    private GameObject BuildActionButton(
        RectTransform parent,
        Sprite rounded,
        string label,
        UnityEngine.Events.UnityAction onClick,
        out RectTransform buttonRect)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        buttonRect = buttonObject.GetComponent<RectTransform>();
        Place(buttonRect, 0f, 0f, 190f, 56f);

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        image.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.45f);

        Button button = buttonObject.GetComponent<Button>();

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;

        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText("Label", buttonObject.transform, label, 24f, Color.white,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Stretch(text.rectTransform, 8f, 4f, 8f, 4f);

        return buttonObject;
    }

    // ------------------------------------------------------------------
    // AUXILIARES DE INTERFAZ
    // ------------------------------------------------------------------
    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static Image CreateImage(string objectName, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private static TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        string content,
        float size,
        Color color,
        TextAlignmentOptions alignment,
        FontStyles style = FontStyles.Normal)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;

#pragma warning disable CS0618
        text.enableWordWrapping = true;
#pragma warning restore CS0618

        return text;
    }

    // Coloca un elemento desde la esquina superior izquierda de su padre
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    // Estira un elemento para llenar a su padre, con márgenes
    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}