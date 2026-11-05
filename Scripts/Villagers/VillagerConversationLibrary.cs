
using System;
using System.Collections.Generic;
using UnityEngine;

public enum SocialRequirement
{
    Cualquiera,
    Extrovertido,
    Introvertido
}

public enum EnergyRequirement
{
    Cualquiera,
    Tranquilo,
    Energico
}

public enum OutlookRequirement
{
    Cualquiera,
    Optimista,
    Pesimista
}

public enum DecisionRequirement
{
    Cualquiera,
    Emocional,
    Logico
}

public enum TrustRequirement
{
    Cualquiera,
    Confiado,
    Desconfiado
}

[Serializable]
public class VillagerConversation
{
    public string nodeName;

    [Header("Rasgos requeridos")]
    public SocialRequirement social;
    public EnergyRequirement energy;
    public OutlookRequirement outlook;
    public DecisionRequirement decision;
    public TrustRequirement trust;
}

[CreateAssetMenu(
    fileName = "VillagerConversationLibrary",
    menuName = "Dialogue/Villager Conversation Library"
)]
public class VillagerConversationLibrary : ScriptableObject
{
    public List<VillagerConversation> conversations =
        new List<VillagerConversation>();
}
