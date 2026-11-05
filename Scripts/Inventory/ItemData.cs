using UnityEngine;

public enum PlaceholderShape
{
    Sphere,
    Cube
}

/// <summary>
/// Datos fijos de un tipo de objeto. Se crea desde el menú
/// Create > Inventory > Item Data.
/// </summary>
[CreateAssetMenu(fileName = "Item_New", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Identidad")]
    [Tooltip("Identificador único y estable (lo usará el guardado). No lo cambies una vez usado.")]
    public string id = "item_new";
    public string displayName = "Objeto";
    [TextArea(2, 4)] public string description;

    [Header("Apilado")]
    [Tooltip("Cuántas unidades caben en una casilla. 1 = no se apila.")]
    [Min(1)] public int maxStack = 10;

    [Header("Icono")]
    [Tooltip("Si se deja vacío se genera un icono provisional con la forma y el color de abajo.")]
    public Sprite icon;

    [Header("Aspecto provisional (para pruebas)")]
    public PlaceholderShape placeholderShape = PlaceholderShape.Sphere;
    public Color placeholderColor = Color.white;

    [Header("Mundo")]
    [Tooltip("Prefab que aparece en el mundo al soltar el objeto. Si se deja vacío se crea una esfera o un cubo.")]
    public GameObject worldPrefab;

    [Header("Decoración")]
    [Tooltip("Si se activa, el objeto se puede colocar en el mundo desde el inventario (muebles, etc.).")]
    public bool placeable = false;

    [Tooltip("Casillas de la cuadrícula que ocupa: X = ancho, Y = fondo (mirando hacia delante).")]
    public Vector2Int footprint = new Vector2Int(1, 1);

    [Tooltip("Altura del objeto colocado, en metros. Con el ancho y el fondo forma su hitbox.")]
    [Min(0.1f)] public float placedHeight = 1f;

    [Tooltip("Prefab que se coloca en el mundo. Si se deja vacío se crea un bloque provisional del tamaño del objeto.")]
    public GameObject placedPrefab;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            id = name.ToLowerInvariant().Replace(' ', '_');
        }

        footprint.x = Mathf.Max(1, footprint.x);
        footprint.y = Mathf.Max(1, footprint.y);
    }
}