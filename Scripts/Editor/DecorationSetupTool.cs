using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prepara de un clic todo lo necesario para probar el sistema de decoración.
/// Este archivo debe estar dentro de una carpeta llamada "Editor".
/// Se puede ejecutar varias veces sin duplicar nada.
/// </summary>
public static class DecorationSetupTool
{
    private const string ItemsFolder = "Assets/Data/Items";

    [MenuItem("Tools/Decoration/Preparar decoración de prueba")]
    public static void Setup()
    {
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Items");

        // --- Muebles de prueba (bloques con la forma de su huella) ---
        ItemData table = GetOrCreate("Item_Mesa", d =>
        {
            d.id = "furniture_table";
            d.displayName = "Mesa";
            d.description = "Una mesa grande. Ocupa dos casillas de ancho.";
            d.maxStack = 5;
            d.placeholderShape = PlaceholderShape.Cube;
            d.placeholderColor = new Color(0.65f, 0.45f, 0.28f);
            d.placeable = true;
            d.footprint = new Vector2Int(2, 1);
            d.placedHeight = 0.8f;
        });

        ItemData chair = GetOrCreate("Item_Silla", d =>
        {
            d.id = "furniture_chair";
            d.displayName = "Silla";
            d.description = "Una silla sencilla. Ocupa una sola casilla.";
            d.maxStack = 5;
            d.placeholderShape = PlaceholderShape.Cube;
            d.placeholderColor = new Color(0.35f, 0.55f, 0.85f);
            d.placeable = true;
            d.footprint = new Vector2Int(1, 1);
            d.placedHeight = 0.9f;
        });

        ItemData sofa = GetOrCreate("Item_Sofa", d =>
        {
            d.id = "furniture_sofa";
            d.displayName = "Sofá";
            d.description = "Un sofá largo. Ocupa tres casillas de ancho.";
            d.maxStack = 2;
            d.placeholderShape = PlaceholderShape.Cube;
            d.placeholderColor = new Color(0.7f, 0.4f, 0.75f);
            d.placeable = true;
            d.footprint = new Vector2Int(3, 1);
            d.placedHeight = 0.8f;
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // --- Sistema de decoración en la escena ---
        if (Object.FindFirstObjectByType<DecorationPlacer>() == null)
        {
            GameObject system = new GameObject("DecorationSystem");
            system.AddComponent<DecorationPlacer>();
            Undo.RegisterCreatedObjectUndo(system, "Crear sistema de decoración");
        }

        // --- Objetos de prueba en el inventario inicial ---
        Inventory inventory = Object.FindFirstObjectByType<Inventory>();

        if (inventory != null)
        {
            AddStartingItem(inventory, table, 2);
            AddStartingItem(inventory, chair, 4);
            AddStartingItem(inventory, sofa, 1);
        }
        else
        {
            Debug.LogWarning(
                "No hay ningún Inventory en la escena. Ejecuta antes Tools > Inventory > " +
                "Preparar escena de prueba, o añade los muebles al inventario a mano."
            );
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "Decoración preparada. Dale a Play, abre la bolsa con I, selecciona un mueble " +
            "y pulsa F (o el botón Colocar)."
        );
    }

    // Añade un objeto a la lista de objetos iniciales del inventario (si no estaba ya)
    private static void AddStartingItem(Inventory inventory, ItemData item, int amount)
    {
        SerializedObject serialized = new SerializedObject(inventory);
        SerializedProperty list = serialized.FindProperty("startingItems");

        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty existing = list.GetArrayElementAtIndex(i).FindPropertyRelative("item");

            if (existing.objectReferenceValue == item)
            {
                return;
            }
        }

        int index = list.arraySize;
        list.arraySize = index + 1;

        SerializedProperty entry = list.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("item").objectReferenceValue = item;
        entry.FindPropertyRelative("amount").intValue = amount;

        serialized.ApplyModifiedProperties();
    }

    private static ItemData GetOrCreate(string fileName, System.Action<ItemData> setup)
    {
        string path = $"{ItemsFolder}/{fileName}.asset";
        ItemData existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);

        if (existing != null)
        {
            return existing;
        }

        ItemData data = ScriptableObject.CreateInstance<ItemData>();
        setup(data);
        AssetDatabase.CreateAsset(data, path);

        return data;
    }

    private static void EnsureFolder(string parent, string folderName)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{folderName}"))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
