using UnityEngine;

/// <summary>
/// Decide QUÉ GRUPO de conversaciones lanzar (Chat, WeatherComment, TimeOfDay, Event).
/// La elección de la conversación concreta dentro del grupo la hace Yarn
/// con los when: de cada nodo.
/// </summary>
public class VillagerConversationSelector : MonoBehaviour
{
    // Títulos de los node groups. Deben existir en los .yarn.
    public const string ChatGroup = "Chat";
    public const string WeatherGroup = "WeatherComment";
    public const string TimeOfDayGroup = "TimeOfDay";
    public const string EventGroup = "Event";

    [SerializeField] private WorldState worldState;

    [Header("Probabilidades (0 a 1)")]
    [Tooltip("Probabilidad de hablar del evento activo (si lo hay).")]
    [SerializeField, Range(0f, 1f)] private float eventChance = 0.7f;

    [Tooltip("Probabilidad de comentar el tiempo atmosférico.")]
    [SerializeField, Range(0f, 1f)] private float weatherChance = 0.35f;

    [Tooltip("Probabilidad de comentar la hora del día.")]
    [SerializeField, Range(0f, 1f)] private float timeOfDayChance = 0.2f;

    public string SelectConversation()
    {
        if (worldState != null)
        {
            if (worldState.HasActiveEvent && Random.value < eventChance)
            {
                return EventGroup;
            }

            if (Random.value < weatherChance)
            {
                return WeatherGroup;
            }

            if (Random.value < timeOfDayChance)
            {
                return TimeOfDayGroup;
            }
        }

        return ChatGroup;
    }
}