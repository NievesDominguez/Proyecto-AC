using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Lo que InventoryUI necesita saber de lo que ocurre en cada casilla.
/// </summary>
public interface IInventorySlotHandler
{
    /// <summary>La casilla quedó seleccionada (teclado, mando o ratón encima).</summary>
    void OnSlotSelected(int index);

    /// <summary>Clic izquierdo, o Enter/E con la casilla seleccionada.</summary>
    void OnSlotActivated(int index);

    /// <summary>Clic derecho sobre la casilla.</summary>
    void OnSlotRightClicked(int index);

    /// <summary>Empieza un arrastre con el ratón desde esta casilla.</summary>
    void OnSlotBeginDrag(int index);

    /// <summary>Termina el arrastre (puede haber acabado sobre otra casilla).</summary>
    void OnSlotEndDrag(int index, PointerEventData eventData);
}

/// <summary>
/// Una casilla de la cuadrícula del inventario: icono, cantidad, resaltado
/// y estado de "levantada" cuando su objeto se está moviendo.
/// </summary>
public class InventorySlotView :
    MonoBehaviour,
    ISelectHandler,
    IPointerEnterHandler,
    IPointerClickHandler,
    ISubmitHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    private const float LiftedAlpha = 0.28f;

    public int Index { get; private set; }

    private Image background;
    private Image icon;
    private TMP_Text amountText;
    private IInventorySlotHandler handler;

    private Color normalColor;
    private Color highlightColor;
    private Color tint = Color.white;
    private bool highlighted;
    private bool lifted;

    public void Setup(
        int index,
        Image backgroundImage,
        Image iconImage,
        TMP_Text amountLabel,
        Color normal,
        Color highlight,
        IInventorySlotHandler slotHandler)
    {
        Index = index;
        background = backgroundImage;
        icon = iconImage;
        amountText = amountLabel;
        normalColor = normal;
        highlightColor = highlight;
        handler = slotHandler;

        Clear();
        ApplyColor();
    }

    public void SetContent(Sprite sprite, Color newTint, int amount)
    {
        tint = newTint;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
        amountText.text = amount > 1 ? $"x{amount}" : string.Empty;

        ApplyLift();
    }

    public void Clear()
    {
        tint = Color.white;
        icon.sprite = null;
        icon.enabled = false;
        amountText.text = string.Empty;

        ApplyLift();
    }

    public void SetHighlighted(bool value)
    {
        highlighted = value;
        ApplyColor();
    }

    /// <summary>Atenúa el contenido mientras el objeto "está en la mano".</summary>
    public void SetLifted(bool value)
    {
        lifted = value;
        ApplyLift();
    }

    // ------------------------------------------------------------------
    // EVENTOS
    // ------------------------------------------------------------------
    public void OnSelect(BaseEventData eventData)
    {
        handler?.OnSlotSelected(Index);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Pasar el ratón por encima selecciona la casilla, igual que con el teclado
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            handler?.OnSlotActivated(Index);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            handler?.OnSlotRightClicked(Index);
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        handler?.OnSlotActivated(Index);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            handler?.OnSlotBeginDrag(Index);
        }
    }

    // Hace falta implementarlo para que Unity detecte la casilla como "arrastrable"
    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            handler?.OnSlotEndDrag(Index, eventData);
        }
    }

    // ------------------------------------------------------------------
    // ASPECTO
    // ------------------------------------------------------------------
    private void Update()
    {
        // La casilla seleccionada "crece" un poco, con suavizado
        float target = highlighted ? 1.08f : 1f;
        float blend = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);

        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, blend);
    }

    private void ApplyColor()
    {
        if (background != null)
        {
            background.color = highlighted ? highlightColor : normalColor;
        }
    }

    private void ApplyLift()
    {
        if (icon == null || amountText == null)
        {
            return;
        }

        Color iconColor = tint;
        iconColor.a = lifted ? LiftedAlpha : 1f;
        icon.color = iconColor;

        amountText.alpha = lifted ? LiftedAlpha : 1f;
    }
} 