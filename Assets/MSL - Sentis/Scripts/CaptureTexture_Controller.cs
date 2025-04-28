using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CaptureTexture_Controller : MonoBehaviour
{
    private WebCamTexture _webcamTexture;
    private Texture2D _textureResult;
    private Texture2D _resizedTexture;
    private bool _cameraPermissionGranted = false;

    [SerializeField] Renderer _mirror;

    public void Awake()
    {
        StartCoroutine(RequestCameraPermission());
    }

    private IEnumerator RequestCameraPermission()
    {
        // Request camera permission
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            _cameraPermissionGranted = true;
            InitializeCamera();
        }
        else
        {
            Debug.LogError("Camera permission not granted");
        }
    }

    private void InitializeCamera()
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

    public bool IsCameraAvailable()
    {
        return _cameraPermissionGranted && _webcamTexture != null && _webcamTexture.isPlaying;
    }

    public Texture2D toTexture2D()
    {
        if (!IsCameraAvailable())
            return null;

        if (_textureResult != null)
        {
            Destroy(_textureResult);
            _textureResult = null;
            Destroy(_resizedTexture);
            _resizedTexture = null;
        }

        // Crear una nueva textura 2D con el mismo tamaño que la WebCamTexture
        _textureResult = new Texture2D(_webcamTexture.width, _webcamTexture.height, TextureFormat.RGB24, false);

        // Copiar los píxeles desde WebCamTexture a Texture2D
        _textureResult.SetPixels(_webcamTexture.GetPixels());
        _textureResult.Apply();

        _mirror.material.mainTexture = _textureResult;

        // Redimensionar la textura a 256x256 para el input del modelo
        _resizedTexture = new Texture2D(256, 256);

        // Crear una RenderTexture temporal para redimensionar
        RenderTexture rt = RenderTexture.GetTemporary(256, 256);
        Graphics.Blit(_textureResult, rt);

        // Leer la textura redimensionada
        RenderTexture.active = rt;
        _resizedTexture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        _resizedTexture.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return _resizedTexture;
    }

    void OnDestroy()
    {
        if (_webcamTexture != null)
        {
            _webcamTexture.Stop();
        }
    }
}
