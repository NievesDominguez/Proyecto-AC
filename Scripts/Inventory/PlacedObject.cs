using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marca un objeto colocado en el mundo con el sistema de decoración.
/// Guarda de qué objeto se trata y cómo está colocado (lo usarán más adelante
/// el guardado y la opción de recogerlo de nuevo).
/// </summary>
public class PlacedObject : MonoBehaviour
{
    private static readonly List<PlacedObject> all = new List<PlacedObject>();

    /// <summary>Todos los objetos colocados que hay ahora mismo en la escena.</summary>
    public static IReadOnlyList<PlacedObject> All => all;

    public ItemData Item { get; private set; }

    /// <summary>Giros de 90 grados respecto a la orientación base.</summary>
    public int QuarterTurns { get; private set; }

    /// <summary>Casillas que ocupa ya con el giro aplicado (X = ancho en el mundo, Y = fondo).</summary>
    public Vector2Int OccupiedCells { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        all.Clear();
    }

    private void OnEnable()
    {
        if (!all.Contains(this))
        {
            all.Add(this);
        }
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    public void Initialize(ItemData item, int quarterTurns, Vector2Int occupiedCells)
    {
        Item = item;
        QuarterTurns = quarterTurns;
        OccupiedCells = occupiedCells;
    }
}
