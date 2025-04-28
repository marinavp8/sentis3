using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Networking;

[RequireComponent(typeof(CaptureTexture_Controller))]
public class NewMonoBehaviourScript : MonoBehaviour
{
    [Header("WebCam Texture")]
    private CaptureTexture_Controller _capture = null;
    private Renderer _mirror = null;
    private Texture2D _texture = null;
    [SerializeField] private bool mirrorMode = true;

    [Header("UI Elements")]
    [SerializeField] private Button startButton;
    [SerializeField] private GameObject permissionPanel;
    [SerializeField] private Text statusText;
    private bool isModelStarted = false;

    [Header("FPS + Inference Time ms")]
    Fps_Counter_Controller _fps;

    [Header("Sentis Variables")]
    private Model sourceModel;
    private Worker worker;
    private float lastProcessTime = 0f;
    private Tensor tensorEntrada;
    private int[] tensorBuffer;
    private Tensor<int> inputTensor;

    [Header("Temporary Spheres")]
    [SerializeField] private Material tempSphereMaterial;
    [SerializeField] private float tempSphereSize = 0.05f;
    [SerializeField] private float tempSphereLifetime = 1.0f;
    [SerializeField] private Color tempSphereColor = Color.red;

    public Dictionary<string, GameObject> keypoints;
    [SerializeField] Material keypointMaterial;
    [SerializeField] float keypointSize = 0.02f;
    [SerializeField] Material lineMaterial;
    private LineRenderer[] connections;
    [SerializeField] float pointSize2D = 10f;
    [SerializeField] Color pointColor2D = Color.yellow;
    private Dictionary<string, Vector2> keypoints2D = new Dictionary<string, Vector2>();
    private Dictionary<string, float> keypointConfidence = new Dictionary<string, float>();
    private Texture2D pointTexture;
    [SerializeField] float confidenceThreshold = 0.4f;

    private readonly (string, string)[] skeletonConnections = new[]
    {
        ("nose", "leftEye"), ("nose", "rightEye"),
        ("leftEye", "leftEar"), ("rightEye", "rightEar"),
        ("nose", "leftShoulder"), ("nose", "rightShoulder"),
        ("leftShoulder", "rightShoulder"),
        ("leftShoulder", "leftElbow"), ("leftElbow", "leftWrist"),
        ("rightShoulder", "rightElbow"), ("rightElbow", "rightWrist"),
        ("leftShoulder", "leftHip"), ("rightShoulder", "rightHip"),
        ("leftHip", "rightHip"),
        ("leftHip", "leftKnee"), ("leftKnee", "leftAnkle"),
        ("rightHip", "rightKnee"), ("rightKnee", "rightAnkle")
    };

    const string keypointNames = "nose,leftEye,rightEye,leftEar,rightEar,leftShoulder,rightShoulder,leftElbow,rightElbow,leftWrist,rightWrist,leftHip,rightHip,leftKnee,rightKnee,leftAnkle,rightAnkle";

    public void Awake()
    {
        _capture = GetComponent<CaptureTexture_Controller>();
        _mirror = GetComponent<Renderer>();

        // Initialize UI elements
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartModel);
            startButton.gameObject.SetActive(false);
        }

        if (permissionPanel != null)
        {
            permissionPanel.SetActive(true);
        }

        if (statusText != null)
        {
            statusText.text = "Esperando permisos de cámara...";
        }
    }

    private void StartModel()
    {
        isModelStarted = true;
        if (permissionPanel != null)
        {
            permissionPanel.SetActive(false);
        }
        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }
        if (statusText != null)
        {
            statusText.text = "Modelo iniciado";
        }
    }

    void Start()
    {
        _fps = new();

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        if (GetComponent<MeshFilter>().mesh.name == "Quad")
        {
            transform.localScale = new Vector3(4, 3, 1); // Aspecto 4:3 común en webcams
        }

        if (mirrorMode)
        {
            _mirror.material.mainTextureScale = new Vector2(-1, 1);
        }

        keypoints = new Dictionary<string, GameObject>();
        string[] keypointNameArray = keypointNames.Split(',');
        foreach (string keypointName in keypointNameArray)
        {
            if (keypointName != "rect") // Ignoramos el keypoint "rect"
            {
                keypoints[keypointName] = CreateKeypointSphere(keypointName);
            }
        }

        CreateConnections();

        // Iniciamos la carga del modelo
        StartCoroutine(InitializeModelAndCamera());
    }

    private IEnumerator InitializeModelAndCamera()
    {
#if UNITY_EDITOR
        Debug.Log("cargando desde el editor");
        string PATH = "Assets/StreamingAssets/singlepose-thunder.sentis";
        Debug.Log($"Ruta del modelo: {PATH}");
        
        if (!System.IO.File.Exists(PATH))
        {
            Debug.LogError($"El archivo del modelo no existe en: {PATH}");
            yield break;
        }

        try
        {
            sourceModel = ModelLoader.Load(PATH);
            if (sourceModel == null)
            {
                Debug.LogError($"No se pudo cargar el modelo desde: {PATH}");
                yield break;
            }
            Debug.Log("Modelo cargado correctamente");

            try
            {
                worker = new Worker(sourceModel, BackendType.GPUCompute);
                Debug.Log("Worker GPU creado correctamente");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"No se pudo usar GPU, usando CPU: {e.Message}");
                worker = new Worker(sourceModel, BackendType.CPU);
                Debug.Log("Worker CPU creado correctamente");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al cargar el modelo: {e.Message}\nStackTrace: {e.StackTrace}");
            yield break;
        }
#elif UNITY_WEBGL
        Debug.Log("cargando desde el webgl");
        string PATH = "StreamingAssets/singlepose-thunder.sentis";
        Debug.Log($"Intentando cargar modelo desde: {PATH}");
        
        using (UnityWebRequest www = UnityWebRequest.Get(PATH))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error cargando el modelo: {www.error}");
                if (statusText != null)
                {
                    statusText.text = "Error: No se pudo cargar el modelo";
                }
                yield break;
            }

            try
            {
                sourceModel = ModelLoader.Load(www.downloadHandler.data);
                if (sourceModel == null)
                {
                    Debug.LogError("El modelo se cargó como null");
                    yield break;
                }
                Debug.Log("Modelo cargado correctamente");
                
                try
                {
                    worker = new Worker(sourceModel, BackendType.GPUCompute);
                    Debug.Log("Worker GPU creado correctamente");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"No se pudo usar GPU, usando CPU: {e.Message}");
                    worker = new Worker(sourceModel, BackendType.CPU);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error al cargar el modelo: {e.Message}\nStackTrace: {e.StackTrace}");
                yield break;
            }
        }
#else
        string PATH = System.IO.Path.Combine(Application.streamingAssetsPath, "singlepose-thunder.sentis");
#endif

        // Esperamos a que la cámara esté disponible
        float timeout = 10f; // 10 segundos de timeout
        float elapsed = 0f;

        while (!_capture.IsCameraAvailable() && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            Debug.Log($"Esperando a que la cámara esté disponible... {elapsed:F1}s");
            yield return null;
        }

        if (!_capture.IsCameraAvailable())
        {
            Debug.LogError("Timeout esperando a que la cámara esté disponible");
            if (statusText != null)
            {
                statusText.text = "Error: Timeout esperando cámara";
            }
            yield break;
        }

        if (sourceModel == null || worker == null)
        {
            Debug.LogError("El modelo o el worker no se inicializaron correctamente");
            yield break;
        }

        try
        {
            // Ahora que la cámara está disponible, intentamos obtener la textura
            _texture = _capture.toTexture2D();
            if (_texture != null)
            {
                _mirror.material.mainTexture = _texture;

                pointTexture = new Texture2D(1, 1);
                pointTexture.SetPixel(0, 0, Color.white);
                pointTexture.Apply();

                // Initialize buffer and tensor
                tensorBuffer = new int[_texture.width * _texture.height * 3];
                TensorShape formaTensor = new TensorShape(1, _texture.height, _texture.width, 3);
                tensorEntrada = new Tensor<int>(formaTensor, true);

                Debug.Log("Cámara y modelo inicializados correctamente");
            }
            else
            {
                Debug.LogError("Failed to get texture from capture after camera was available");
                if (statusText != null)
                {
                    statusText.text = "Error: No se pudo obtener imagen de la cámara";
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error initializing camera: {e.Message}\nStackTrace: {e.StackTrace}");
            if (statusText != null)
            {
                statusText.text = "Error al inicializar la cámara";
            }
        }
    }

    private bool LoadModelFromPath(string path)
    {
        try
        {
            sourceModel = ModelLoader.Load(path);
            if (sourceModel == null)
            {
                Debug.LogError($"Failed to load model at path: {path}");
                if (statusText != null)
                {
                    statusText.text = "Error: No se pudo cargar el modelo";
                }
                return false;
            }

            Debug.Log($"Model loaded successfully from: {path}");

            // Initialize worker with fallback
            try
            {
                worker = new Worker(sourceModel, BackendType.GPUCompute);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"GPU not available, falling back to CPU: {e.Message}");
                worker = new Worker(sourceModel, BackendType.CPU);
            }
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error loading model: {e.Message}\nStackTrace: {e.StackTrace}");
            if (statusText != null)
            {
                statusText.text = "Error al cargar el modelo";
            }
            return false;
        }
    }

    void OnDestroy()
    {
        if (tensorEntrada != null)
        {
            tensorEntrada.Dispose();
        }
    }

    private GameObject CreateKeypointSphere(string name)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.parent = this.transform;
        sphere.transform.localScale = new Vector3(keypointSize, keypointSize, keypointSize);
        var renderer = sphere.GetComponent<Renderer>();
        renderer.material = keypointMaterial;

        // Remove any collider component if it exists
        var collider = sphere.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        return sphere;
    }

    private void CreateConnections()
    {
        connections = new LineRenderer[skeletonConnections.Length];
        for (int i = 0; i < skeletonConnections.Length; i++)
        {
            GameObject lineObj = new GameObject($"Connection_{i}");
            lineObj.transform.parent = this.transform;
            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.material = lineMaterial;
            line.startWidth = line.endWidth = keypointSize * 0.5f;
            line.positionCount = 2;
            connections[i] = line;
        }
    }

    private void UpdateConnections()
    {
        for (int i = 0; i < skeletonConnections.Length; i++)
        {
            var connection = skeletonConnections[i];
            var line = connections[i];

            if (keypoints.TryGetValue(connection.Item1, out GameObject start) &&
                keypoints.TryGetValue(connection.Item2, out GameObject end))
            {
                if (start.activeSelf && end.activeSelf)
                {
                    line.SetPosition(0, start.transform.position);
                    line.SetPosition(1, end.transform.position);
                    line.enabled = true;
                }
                else
                {
                    line.enabled = false;
                }
            }
        }
    }

    bool _infection = false;

    void Update()
    {
        if (!isModelStarted)
        {
            // Check if camera is available
            if (_capture != null && _capture.IsCameraAvailable())
            {
                if (permissionPanel != null)
                {
                    permissionPanel.SetActive(false);
                }
                if (startButton != null)
                {
                    startButton.gameObject.SetActive(true);
                }
                if (statusText != null)
                {
                    statusText.text = "Cámara disponible. Presiona Iniciar para comenzar.";
                }
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
            _infection = !_infection;

        _fps.Calcule_FPS();
        if (_infection)
        {
            if (Time.time - lastProcessTime < 0.1f)
                return;
        }

        _fps.StartWatch();

        lastProcessTime = Time.time;

        _texture = _capture.toTexture2D();
        _mirror.material.mainTexture = _texture;
        try
        {
            // 1. Obtener los datos de la textura como un array de colores
            Color32[] colores = _texture.GetPixels32();
            Logger.Log($"Texture size: {_texture.width}x{_texture.height}, Colors length: {colores.Length}");

            // 2. Actualizar el buffer 
            int indiceTensor = 0;
            for (int y = 0; y < _texture.height; y++)
            {
                for (int x = 0; x < _texture.width; x++)
                {
                    Color32 color = colores[y * _texture.width + x];
                    tensorBuffer[indiceTensor++] = color.r;
                    tensorBuffer[indiceTensor++] = color.g;
                    tensorBuffer[indiceTensor++] = color.b;
                }
            }

            // 3. Crear nuevo tensor cada vez
            TensorShape formaTensor = new TensorShape(1, _texture.height, _texture.width, 3);
            tensorEntrada = new Tensor<int>(formaTensor, tensorBuffer);

            // 4. Usar el tensor en el modelo 
            worker.Schedule(tensorEntrada);

            Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
            Logger.Log("outputTensor shape: " + outputTensor.shape);
            Logger.Log("outputTensor dataType: " + outputTensor.dataType);
            var readableTensor = outputTensor.ReadbackAndClone();
            var outputData = readableTensor.AsReadOnlySpan();
            Logger.Log("outputData length: " + outputData.Length);

            string[] keypointNameArray = keypointNames.Split(',');
            int keypointIndex = 0;
            for (int i = 0; i < keypointNameArray.Length; i++)
            {
                string keypointName = keypointNameArray[i];
                if (keypointName == "rect")
                {
                    continue;
                }

                if (keypoints.ContainsKey(keypointName))
                {
                    int baseIndex = keypointIndex * 3;
                    if (baseIndex + 2 >= outputData.Length)
                    {
                        Logger.LogError($"Index out of bounds for keypoint {keypointName}. Base index: {baseIndex}, Output length: {outputData.Length}");
                        break;
                    }

                    float y = outputData[baseIndex];
                    float x = outputData[baseIndex + 1];
                    float confidence = outputData[baseIndex + 2];

                    if (confidence > confidenceThreshold)
                    {
                        Vector3 rawPosition = new Vector3(
                            (0.5f - x) * transform.localScale.x,
                            (0.5f - y) * transform.localScale.y,
                            -1f
                        );

                        keypoints[keypointName].transform.localPosition = rawPosition;
                        keypoints[keypointName].SetActive(true);

                        // Crear esfera temporal para las manos
                        if (keypointName == "leftWrist" || keypointName == "rightWrist")
                        {
                            CreateTemporarySphere(rawPosition);
                        }

                        Logger.Log($"Keypoint {keypointName}: confidence={confidence}, x={x}, y={y}, pos={rawPosition}");

                        keypoints2D[keypointName] = new Vector2(x, y);
                        keypointConfidence[keypointName] = confidence;
                    }
                    else
                    {
                        keypoints[keypointName].SetActive(false);
                        if (keypoints2D.ContainsKey(keypointName))
                        {
                            keypoints2D.Remove(keypointName);
                            keypointConfidence.Remove(keypointName);
                        }
                    }
                }
                keypointIndex++;
            }

            UpdateConnections();

            readableTensor.Dispose();

            _fps.StopWatch();

        }
        catch (System.Exception e)
        {
            Logger.LogError("Error al generar el tensor Sentis: " + e.Message);
        }
    }

    private void CreateTemporarySphere(Vector3 position)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.parent = this.transform;
        sphere.transform.localPosition = position;
        sphere.transform.localScale = new Vector3(tempSphereSize, tempSphereSize, tempSphereSize);

        var renderer = sphere.GetComponent<Renderer>();
        renderer.material = tempSphereMaterial;
        renderer.material.color = tempSphereColor;

        Destroy(sphere, tempSphereLifetime);
    }
}
