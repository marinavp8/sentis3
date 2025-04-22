using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CaptureTexture_Controller : MonoBehaviour
{
    private WebCamTexture _webcamTexture;
    private Texture2D _textureResult;

    [SerializeField] float brightness = 1.2f; // Ajuste de brillo, > 1 aumenta, < 1 disminuye
    [SerializeField] float contrast = 1.2f;   // Ajuste de contraste, > 1 aumenta, < 1 disminuye

    public void Awake()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length > 0)
        {
            _webcamTexture = new WebCamTexture(devices[0].name, 256, 256);
            _webcamTexture.Play();
        }
        else
        {
            Logger.LogWarning("No se encontraron cámaras disponibles.");
        }
    }

    public Texture2D toTexture2D()
    {
        if (_textureResult != null)
        {
            Destroy(_textureResult);
        }

        WebCamTexture rTex = _webcamTexture as WebCamTexture;

        if (rTex != null)
        {
            // Crear una nueva textura 2D con el mismo tamaño que la WebCamTexture
            _textureResult = new Texture2D(rTex.width, rTex.height, TextureFormat.RGB24, false);

            // Copiar los píxeles desde WebCamTexture a Texture2D
            _textureResult.SetPixels(rTex.GetPixels());
            _textureResult.Apply();

            //_textureResult = new Texture2D(rTex.width, rTex.height);

            // Obtener los colores y ajustar brillo/contraste
            //Color32[] originalColors = _textureResult.GetPixels32();
            //Color32[] adjustedColors = AdjustImageColors(originalColors);

            //// Aplicar los colores ajustados
            //_textureResult.SetPixels32(adjustedColors);
            //_textureResult.Apply();

            // Redimensionar la textura a 256x256 para el input del modelo
            Texture2D resizedTexture = new Texture2D(256, 256);

            // Crear una RenderTexture temporal para redimensionar
            RenderTexture rt = RenderTexture.GetTemporary(256, 256);
            Graphics.Blit(_textureResult, rt);

            // Leer la textura redimensionada
            RenderTexture.active = rt;
            resizedTexture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            resizedTexture.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            _textureResult = resizedTexture;

            Destroy(resizedTexture);
            return _textureResult;
        }
        return null;
    }

    private Color32[] AdjustImageColors(Color32[] originalColors)
    {
        Color32[] adjustedColors = new Color32[originalColors.Length];

        for (int i = 0; i < originalColors.Length; i++)
        {
            // Convertir a float para hacer los cálculos
            float r = originalColors[i].r / 255f;
            float g = originalColors[i].g / 255f;
            float b = originalColors[i].b / 255f;

            // Aplicar brillo
            r *= brightness;
            g *= brightness;
            b *= brightness;

            // Aplicar contraste
            r = (r - 0.5f) * contrast + 0.5f;
            g = (g - 0.5f) * contrast + 0.5f;
            b = (b - 0.5f) * contrast + 0.5f;

            // Asegurar que los valores estén entre 0 y 1
            r = Mathf.Clamp01(r);
            g = Mathf.Clamp01(g);
            b = Mathf.Clamp01(b);

            // Convertir de vuelta a Color32
            adjustedColors[i] = new Color32(
                (byte)(r * 255),
                (byte)(g * 255),
                (byte)(b * 255),
                originalColors[i].a
            );
        }

        return adjustedColors;
    }

    void OnDestroy()
    {
        _webcamTexture.Stop();
    }
}
