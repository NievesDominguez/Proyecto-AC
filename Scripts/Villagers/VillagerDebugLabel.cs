using UnityEngine;

/// <summary>
/// Muestra sobre el aldeano su nombre y la actividad que está haciendo.
/// Se ve aunque el aldeano esté oculto dentro de su casa.
/// Solo para pruebas: se puede quitar cuando haya arte y animaciones.
/// </summary>
[RequireComponent(typeof(VillagerRoutine))]
[RequireComponent(typeof(VillagerPersonality))]
public class VillagerDebugLabel : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.3f, 0f);
    [SerializeField] private bool visible = true;

    private VillagerRoutine routine;
    private VillagerPersonality personality;
    private GUIStyle style;
    private GUIStyle shadowStyle;

    private void Awake()
    {
        routine = GetComponent<VillagerRoutine>();
        personality = GetComponent<VillagerPersonality>();
    }

    private void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        Camera cam = Camera.main;

        if (cam == null)
        {
            return;
        }

        Vector3 screen = cam.WorldToScreenPoint(transform.position + offset);

        if (screen.z <= 0f)
        {
            return;
        }

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                richText = true
            };
            style.normal.textColor = Color.white;

            shadowStyle = new GUIStyle(style);
            shadowStyle.normal.textColor = Color.black;
        }

        string state;

        if (routine.IsPaused)
        {
            state = "hablando";
        }
        else if (routine.CurrentActivity == ActivityType.Sleep)
        {
            state = $"Sleep (hasta las {personality.WakeHour}h)";
        }
        else
        {
            state = $"{routine.CurrentActivity} ({routine.ActivityRemaining:0}s)";
        }

        if (routine.IsInside)
        {
            state += " [en casa]";
        }

        string text = $"{personality.DisplayName}\n{state}";
        Rect rect = new Rect(screen.x - 110f, Screen.height - screen.y - 20f, 220f, 40f);

        // Sombra para que se lea sobre cualquier fondo
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, shadowStyle);
        GUI.Label(rect, text, style);
    }
}