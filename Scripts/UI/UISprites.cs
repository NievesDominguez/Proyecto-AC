using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera por código los sprites que necesita la interfaz (fondos redondeados
/// e iconos provisionales de esfera y cubo), para no depender de imágenes.
/// </summary>
public static class UISprites
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>Rectángulo redondeado blanco, listo para usarse con Image.Type.Sliced.</summary>
    public static Sprite RoundedRect()
    {
        return Get("rounded", () => CreateRoundedRect(64, 20, false));
    }

    /// <summary>Icono provisional de esfera (en tonos de gris, se tiñe con Image.color).</summary>
    public static Sprite Sphere()
    {
        return Get("sphere", CreateSphere);
    }

    /// <summary>Icono provisional de cubo (en tonos de gris, se tiñe con Image.color).</summary>
    public static Sprite Cube()
    {
        return Get("cube", () => CreateRoundedRect(128, 26, true));
    }

    private static Sprite Get(string key, Func<Sprite> factory)
    {
        // El "!= null" de Unity también detecta sprites ya destruidos
        if (cache.TryGetValue(key, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite created = factory();
        cache[key] = created;
        return created;
    }

    private static Texture2D NewTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        return texture;
    }

    private static Sprite MakeSprite(Texture2D texture, Color32[] pixels, int size, Vector4 border)
    {
        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border
        );
    }

    private static Sprite CreateRoundedRect(int size, int radius, bool shaded)
    {
        Texture2D texture = NewTexture(size);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                // Distancia al rectángulo interior (el que queda tras quitar las esquinas)
                float cx = Mathf.Clamp(px, radius, size - radius);
                float cy = Mathf.Clamp(py, radius, size - radius);
                float distance = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));

                float alpha = Mathf.Clamp01(radius - distance + 0.5f);

                // Degradado vertical suave para dar volumen al cubo
                float value = shaded ? Mathf.Lerp(0.62f, 1f, py / size) : 1f;

                pixels[y * size + x] = new Color(value, value, value, alpha);
            }
        }

        // Para el fondo (sin sombreado) se usa 9-slice con el radio como borde
        Vector4 border = shaded ? Vector4.zero : new Vector4(radius, radius, radius, radius);

        return MakeSprite(texture, pixels, size, border);
    }

    private static Sprite CreateSphere()
    {
        const int size = 128;
        float radius = size / 2f - 2f;

        Texture2D texture = NewTexture(size);
        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                float alpha = Mathf.Clamp01(radius - p.magnitude + 0.5f);

                Vector2 n = p / radius; // -1..1, con la Y hacia arriba

                // Luz desde arriba a la izquierda
                float light = Mathf.Clamp01(0.5f + 0.5f * (-n.x * 0.5f + n.y * 0.7f));
                float shade = Mathf.Lerp(0.6f, 1f, light);

                // Pequeño brillo
                float spec = Mathf.Clamp01(1f - new Vector2(n.x + 0.35f, n.y - 0.4f).magnitude * 3.2f);
                float value = Mathf.Clamp01(shade + spec * 0.35f);

                pixels[y * size + x] = new Color(value, value, value, alpha);
            }
        }

        return MakeSprite(texture, pixels, size, Vector4.zero);
    }
}
