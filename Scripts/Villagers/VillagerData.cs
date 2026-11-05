using System.Collections.Generic;
using UnityEngine;

public enum Peculiarity
{
    Ninguna,
    Supersticioso,
    Cotilla,
    Despistado,
    ColeccionistaCompulsivo,
    Dramatico,
    Bromista,
    Noctambulo,
    Manitas,
    Melomano,
    Sonador,
    Competitivo,
    Rencoroso,
    Generoso,
    Acaparador,
    Miedoso,
    Excentrico
}

public enum Hobby
{
    Jardineria,
    Exploracion,
    Musica,
    Fabricacion,
    Decoracion,
    Coleccionismo
}

/// <summary>
/// Datos fijos de un aldeano. Se crea desde el menú
/// Create > Villagers > Villager Data.
/// </summary>
[CreateAssetMenu(fileName = "Villager_New", menuName = "Villagers/Villager Data")]
public class VillagerData : ScriptableObject
{
    [Header("Identidad")]
    public string displayName = "Nuevo vecino";
    public string species = "Sin definir";
    [TextArea(2, 4)] public string description;

    [Header("Personalidad (cinco rasgos)")]
    public SocialTrait social;
    public EnergyTrait energy;
    public OutlookTrait outlook;
    public DecisionTrait decision;
    public TrustTrait trust;

    [Header("Peculiaridad")]
    public Peculiarity peculiarity = Peculiarity.Ninguna;

    [Header("Gustos")]
    public List<Hobby> hobbies = new List<Hobby>();
    [Tooltip("Etiquetas de objetos que le gustan (para regalos, más adelante).")]
    public List<string> likedTags = new List<string>();
    [Tooltip("Etiquetas de objetos que no le gustan.")]
    public List<string> dislikedTags = new List<string>();

    [Header("Horario")]
    [Range(0, 23)] public int wakeHour = 7;
    [Range(0, 23)] public int bedtimeHour = 23;

    /// <summary>Indica si a esa hora le toca descansar.</summary>
    public bool IsAsleepAt(int hour)
    {
        if (wakeHour == bedtimeHour) return false;

        // Ejemplo normal: acostarse a las 23 y despertar a las 7
        if (bedtimeHour > wakeHour) return hour >= bedtimeHour || hour < wakeHour;

        // Ejemplo noctámbulo: acostarse a las 5 y despertar a las 14
        return hour >= bedtimeHour && hour < wakeHour;
    }

    /// <summary>Horas transcurridas desde que se despertó.</summary>
    public int HoursSinceWake(int hour)
    {
        return (hour - wakeHour + 24) % 24;
    }
}
