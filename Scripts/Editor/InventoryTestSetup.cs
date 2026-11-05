using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prepara de un clic todo lo necesario para probar el inventario.
/// Este archivo debe estar dentro de una carpeta llamada "Editor".
/// </summary>
public static class InventoryTestSetup
{
    private const string ItemsFolder = "Assets/Data/Items";

    [MenuItem("Tools/Inventory/Preparar escena de prueba")]
    public static void SetupScene()
    {
        EnsureFolder("Assets", "Data");
        EnsureFolder("Assets/Data", "Items");

        // --- Objetos de prueba (datos) ---
        ItemData redSphere = GetOrCreate("Item_EsferaRoja", d =>
        {
            d.id = "sphere_red";
            d.displayName = "Esfera roja";
            d.description = "Una esfera lisa y brillante. Parece vibrar un poco cuando la sostienes.";
            d.maxStack = 10;
            d.placeholderShape = PlaceholderShape.Sphere;
            d.placeholderColor = new Color(0.95f, 0.30f, 0.30f);
        });

        ItemData blueSphere = GetOrCreate("Item_EsferaAzul", d =>
        {
            d.id = "sphere_blue";
            d.displayName = "Esfera azul";
            d.description = "Fría al tacto. Dentro parece moverse algo parecido a un líquido.";
            d.maxStack = 10;
            d.placeholderShape = PlaceholderShape.Sphere;
            d.placeholderColor = new Color(0.30f, 0.55f, 0.95f);
        });

        ItemData greenCube = GetOrCreate("Item_CuboVerde", d =>
        {
            d.id = "cube_green";
            d.displayName = "Cubo verde";
            d.description = "Un cubo perfecto de un material desconocido. Pesa menos de lo que aparenta.";
            d.maxStack = 5;
            d.placeholderShape = PlaceholderShape.Cube;
            d.placeholderColor = new Color(0.35f, 0.85f, 0.45f);
        });

        ItemData goldCube = GetOrCreate("Item_CuboDorado", d =>
        {
            d.id = "cube_gold";
            d.displayName = "Cubo dorado";
            d.description = "Un objeto raro y muy valioso. Solo se puede llevar uno.";
            d.maxStack = 1;
            d.placeholderShape = PlaceholderShape.Cube;
            d.placeholderColor = new Color(0.98f, 0.80f, 0.25f);
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // --- Sistema de inventario ---
        Inventory inventory = Object.FindFirstObjectByType<Inventory>();

        if (inventory == null)
        {
            GameObject system = new GameObject("InventorySystem");
            inventory = system.AddComponent<Inventory>();
            Undo.RegisterCreatedObjectUndo(system, "Crear inventario");
        }

        if (inventory.GetComponent<InventoryUI>() == null)
        {
            Undo.AddComponent<InventoryUI>(inventory.gameObject);
        }

        // --- Objetos repartidos por el mundo ---
        ItemData[] items = { redSphere, blueSphere, greenCube, goldCube };
        int count = 12;

        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * Mathf.PI * 2f;
            Vector3 position = new Vector3(Mathf.Cos(angle) * 7f, 0.35f, Mathf.Sin(angle) * 7f);

            WorldItem worldItem = WorldItem.Spawn(items[i % items.Length], 1, position);
            Undo.RegisterCreatedObjectUndo(worldItem.gameObject, "Crear objeto de prueba");
        }

        // --- Recogida en el jugador ---
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            if (player.GetComponent<ItemPickupInteractor>() == null)
            {
                Undo.AddComponent<ItemPickupInteractor>(player);
            }
        }
        else
        {
            Debug.LogWarning(
                "No se encontró ningún objeto con el tag 'Player'. " +
                "Añade el componente ItemPickupInteractor al jugador a mano."
            );
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "Inventario preparado: 12 objetos repartidos en un círculo de 7 m. " +
            "Dale a Play, acércate a uno y pulsa E; abre la bolsa con I."
        );
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
