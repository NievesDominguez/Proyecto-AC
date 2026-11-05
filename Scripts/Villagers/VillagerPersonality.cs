using UnityEngine;
using Yarn.Unity;

public enum SocialTrait
{
    Extrovertido,
    Introvertido
}

public enum EnergyTrait
{
    Tranquilo,
    Energico
}

public enum OutlookTrait
{
    Optimista,
    Pesimista
}

public enum DecisionTrait
{
    Emocional,
    Logico
}

public enum TrustTrait
{
    Confiado,
    Desconfiado
}

public class VillagerPersonality : MonoBehaviour
{
    [Header("Datos (opcional)")]
    [Tooltip("Si se asigna, sus valores sustituyen a los de abajo.")]
    public VillagerData data;

    [Header("Personalidad")]
    public SocialTrait social;
    public EnergyTrait energy;
    public OutlookTrait outlook;
    public DecisionTrait decision;
    public TrustTrait trust;
    public Peculiarity peculiarity = Peculiarity.Ninguna;

    public string DisplayName => data != null ? data.displayName : name;
    public int WakeHour => data != null ? data.wakeHour : 7;
    public int BedtimeHour => data != null ? data.bedtimeHour : 23;

    private void Awake()
    {
        ApplyData();
    }

    // Mantiene los campos del Inspector sincronizados al cambiar el VillagerData
    private void OnValidate()
    {
        ApplyData();
    }

    [ContextMenu("Aplicar datos del VillagerData")]
    public void ApplyData()
    {
        if (data == null)
        {
            return;
        }

        social = data.social;
        energy = data.energy;
        outlook = data.outlook;
        decision = data.decision;
        trust = data.trust;
        peculiarity = data.peculiarity;
    }

    public bool IsAsleepAt(int hour)
    {
        if (data != null)
        {
            return data.IsAsleepAt(hour);
        }

        return hour >= 23 || hour < 7;
    }

    public int HoursSinceWake(int hour)
    {
        return (hour - WakeHour + 24) % 24;
    }

    /// <summary>
    /// Escribe los rasgos de este aldeano en las variables de Yarn.
    /// </summary>
    public void PushTo(VariableStorageBehaviour variables)
    {
        variables.SetValue("$social", social.ToString());
        variables.SetValue("$energy", energy.ToString());
        variables.SetValue("$outlook", outlook.ToString());
        variables.SetValue("$decision", decision.ToString());
        variables.SetValue("$trust", trust.ToString());
    }
}