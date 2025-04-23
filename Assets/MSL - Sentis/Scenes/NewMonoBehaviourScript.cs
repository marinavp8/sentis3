using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CaptureTexture_Controller))]
public class NewMonoBehaviourScript : MonoBehaviour
{
    [Header("WebCam Texture")]
    private CaptureTexture_Controller _capture = null;
    private Renderer _mirror = null;
    private Texture2D _texture = null;

    [Header("FPS + Inference Time ms")]
    Fps_Counter_Controller _fps;


    [Header("Sentis Variables")]
    private Model sourceModel;
    private Worker worker;
    private float lastProcessTime = 0f;
    private Tensor tensorEntrada;
    private int[] tensorBuffer;
    private Tensor<int> inputTensor;

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
        string PATH = "";
#if UNITY_EDITOR
        PATH = "Assets/StreamingAssets/singlepose-thunder.sentis";
#endif
#if !UNITY_EDITOR
        PATH = Application.streamingAssetsPath + "/singlepose-thunder.sentis";
#endif
        sourceModel = ModelLoader.Load(PATH);
        Logger.Log("sourceModel: " + sourceModel);

        worker = new Worker(sourceModel, BackendType.GPUCompute);
        _mirror.material.mainTexture = _capture.toTexture2D();

        pointTexture = new Texture2D(1, 1);
        pointTexture.SetPixel(0, 0, Color.white);
        pointTexture.Apply();

        // Inicializar el buffer y el tensor una sola vez
        _texture = _capture.toTexture2D();
        tensorBuffer = new int[_texture.width * _texture.height * 3];
        TensorShape formaTensor = new TensorShape(1, _texture.height, _texture.width, 3);
        // tensorEntrada = new Tensor<int>(formaTensor, tensorBuffer);
        tensorEntrada = new Tensor<int>(formaTensor, true);
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
        if (Input.GetKeyDown(KeyCode.Escape))
            _infection = !_infection;

        _fps.Calcule_FPS();
        // // Inferir cada 0.1 segundos para evitar problemas de rendimiento
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

            // 3. Actualizar el tensor 
            tensorEntrada = new Tensor<int>(tensorEntrada.shape, tensorBuffer);

            // tensorEntrada.Upload(tensorBuffer);

            // worker.Schedule(tensorEntrada);

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



}
