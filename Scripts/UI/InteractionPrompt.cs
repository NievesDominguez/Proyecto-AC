using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Indicador en pantalla ("[E] Hablar", "[E] Coger ..."). Va en un objeto que esté
/// siempre activo (por ejemplo, el Canvas); el panel visible es un hijo que se
/// activa y desactiva.
/// </summary>
public class InteractionPrompt : MonoBehaviour
{
    public static InteractionPrompt Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text label;

    [Tooltip("Texto que se muestra cuando quien lo pide no indica otro.")]
    [SerializeField] private string text = "[E] Hablar";

    // Quién está pidiendo que se muestre y con qué texto.
    private readonly Dictionary<Object, string> requesters = new Dictionary<Object, string>();

    // Quién manda sobre el texto mientras hay varios pidiéndolo a la vez
    private Object active;

    private void Awake()
    {
        Instance = this;
        Refresh();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Pide mostrar u ocultar el indicador. Se puede llamar cada frame.
    /// customText vacío = texto por defecto.
    /// </summary>
    public void Set(Object owner, bool visible, string customText = null)
    {
        if (visible)
        {
            bool isNew = !requesters.ContainsKey(owner);
            bool textChanged = !isNew && requesters[owner] != customText;

            requesters[owner] = customText;

            if (isNew)
            {
                active = owner;
            }

            if (isNew || textChanged)
            {
                Refresh();
            }
        }
        else if (requesters.Remove(owner))
        {
            if (active == owner)
            {
                active = null;
            }

            Refresh();
        }
    }

    private void Refresh()
    {
        bool any = requesters.Count > 0;

        if (panel != null)
        {
            panel.SetActive(any);
        }

        if (!any || label == null)
        {
            return;
        }

        if (active == null || !requesters.ContainsKey(active))
        {
            foreach (KeyValuePair<Object, string> pair in requesters)
            {
                active = pair.Key;
                break;
            }
        }

        string custom = requesters[active];
        label.text = string.IsNullOrEmpty(custom) ? text : custom;
    }
}