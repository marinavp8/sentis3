using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;

[RequireComponent(typeof(CaptureTexture_Controller))]
public class NewMonoBehaviourScript : MonoBehaviour
{
    private CaptureTexture_Controller _capture = null;
    private Renderer _mirror = null;
    private Texture2D _texture = null;

    private Model sourceModel;
    private Worker worker;
    float lastProcessTime = 0f;
    public Dictionary<string, GameObject> keypoints;
    [SerializeField] Material keypointMaterial;
    [SerializeField] float keypointSize = 0.02f;
    [SerializeField] Material lineMaterial;
    private LineRenderer[] connections;

    public float pointSize2D = 10f;
    public Color pointColor2D = Color.yellow;
    private Dictionary<string, Vector2> keypoints2D = new Dictionary<string, Vector2>();
    private Dictionary<string, float> keypointConfidence = new Dictionary<string, float>();
    private Texture2D pointTexture;

    // Variables para ajuste de imagen
    public float confidenceThreshold = 0.2f;
    public float smoothingFactor = 0.3f;

    private Dictionary<string, Vector3> lastPositions = new Dictionary<string, Vector3>();


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

    var PATH = "Assets/MSL - Sentis/StreamingAssets/singlepose-thunder.sentis";
        sourceModel = ModelLoader.Load(PATH);
        Debug.Log("sourceModel: " + sourceModel);

        worker = new Worker(sourceModel, BackendType.GPUCompute);
        _mirror.material.mainTexture = _capture.toTexture2D();

        pointTexture = new Texture2D(1, 1);
        pointTexture.SetPixel(0, 0, Color.white);
        pointTexture.Apply();
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

    private Vector3 SmoothPosition(Vector3 currentPosition, string keypointName)
    {
        if (!lastPositions.ContainsKey(keypointName))
        {
            lastPositions[keypointName] = currentPosition;
            return currentPosition;
        }

        Vector3 smoothedPosition = Vector3.Lerp(lastPositions[keypointName], currentPosition, smoothingFactor);
        lastPositions[keypointName] = smoothedPosition;
        return smoothedPosition;
    }

    //void OnGUI()
    //{
    //    if (keypoints2D == null || keypoints2D.Count == 0) return;

    //    // Calcular el rectángulo donde se muestra la imagen de la cámara
    //    float aspectRatio = (float)_texture.width / _texture.height;
    //    float scaleHeight = Screen.height;
    //    float scaleWidth = scaleHeight * aspectRatio;
    //    float leftIndent = (Screen.width - scaleWidth) / 2;

    //    Rect cameraRect = new Rect(leftIndent, 0, scaleWidth, scaleHeight);

    //    // Configurar estilo para el texto
    //    GUIStyle style = new GUIStyle(GUI.skin.label);
    //    style.normal.textColor = Color.white;
    //    style.fontSize = 12;
    //    style.fontStyle = FontStyle.Bold;

    //    // Dibujar cada punto
    //    foreach (var kvp in keypoints2D)
    //    {
    //        if (!keypointConfidence.ContainsKey(kvp.Key) || keypointConfidence[kvp.Key] < confidenceThreshold) continue;

    //        // Convertir coordenadas normalizadas a coordenadas de pantalla dentro del viewport de la cámara
    //        float screenX = leftIndent + (kvp.Value.x * scaleWidth);
    //        float screenY = (1f - kvp.Value.y) * scaleHeight; // Invertir Y para que coincida con el sistema 3D

    //        GUI.color = pointColor2D;
    //        GUI.DrawTexture(
    //            new Rect(screenX - pointSize2D / 2, screenY - pointSize2D / 2, pointSize2D, pointSize2D),
    //            pointTexture
    //        );

    //        // Dibujar el nombre del punto y su score
    //        string label = $"{kvp.Key} ({keypointConfidence[kvp.Key]:F2})";
    //        GUI.color = Color.black;
    //        GUI.Label(
    //            new Rect(screenX + pointSize2D / 2 + 1, screenY - pointSize2D / 2 + 1, 150, 20),
    //            label,
    //            style
    //        );
    //        GUI.color = Color.white;
    //        GUI.Label(
    //            new Rect(screenX + pointSize2D / 2, screenY - pointSize2D / 2, 150, 20),
    //            label,
    //            style
    //        );
    //    }
    //}

    void Update()
    {
        // Inferir cada 0.1 segundos para evitar problemas de rendimiento
        if (Time.time - lastProcessTime < 0.1f)
            return;

        lastProcessTime = Time.time;
        _texture = _capture.toTexture2D();
        _mirror.material.mainTexture = _texture;
        try
        {
            // 1. Obtener los datos de la textura como un array de colores
            Color32[] colores = _texture.GetPixels32();
            Debug.Log($"Texture size: {_texture.width}x{_texture.height}, Colors length: {colores.Length}");

            // 2. Crear un array de enteros para los datos del tensor
            int[] datosTensor = new int[_texture.width * _texture.height * 3];

            // 3. Convertir los colores a enteros y llenar el array de datos del tensor
            int indiceTensor = 0;
            for (int y = 0; y < _texture.height; y++)
            {
                for (int x = 0; x < _texture.width; x++)
                {
                    Color32 color = colores[y * _texture.width + x];
                    datosTensor[indiceTensor++] = color.r; // Rojo
                    datosTensor[indiceTensor++] = color.g; // Verde
                    datosTensor[indiceTensor++] = color.b; // Azul
                }
            }
            Debug.Log($"Tensor data size: {datosTensor.Length}");

            // 4. Crear la forma del tensor (1, 256, 256, 3)
            TensorShape formaTensor = new TensorShape(1, _texture.height, _texture.width, 3);
            // 5. Crear el ITensor a partir del array de datos y la forma
            Tensor tensorEntrada = new Tensor<int>(formaTensor, datosTensor);
            // 6. Usar el ITensor en tu modelo Sentis
            // inference
            worker.Schedule(tensorEntrada);
            // Get the output tensor from the model
            Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
            Debug.Log("outputTensor shape: " + outputTensor.shape);
            Debug.Log("outputTensor dataType: " + outputTensor.dataType);
            var readableTensor = outputTensor.ReadbackAndClone();
            var outputData = readableTensor.AsReadOnlySpan();
            Debug.Log("outputData length: " + outputData.Length);

            // Actualizar posiciones de los keypoints
            string[] keypointNameArray = keypointNames.Split(',');
            int keypointIndex = 0;
            for (int i = 0; i < keypointNameArray.Length; i++)
            {
                string keypointName = keypointNameArray[i];
                if (keypointName == "rect")
                {
                    continue; // Saltamos rect sin incrementar el índice
                }

                if (keypoints.ContainsKey(keypointName))
                {
                    // Verificar que no nos salimos de los límites
                    int baseIndex = keypointIndex * 3;
                    if (baseIndex + 2 >= outputData.Length)
                    {
                        Debug.LogError($"Index out of bounds for keypoint {keypointName}. Base index: {baseIndex}, Output length: {outputData.Length}");
                        break;
                    }

                    // Cada keypoint tiene 3 valores (x, y, confidence)
                    float y = outputData[baseIndex];
                    float x = outputData[baseIndex + 1];
                    float confidence = outputData[baseIndex + 2];

                    // Solo actualizamos si la confianza es suficiente
                    if (confidence > confidenceThreshold)
                    {
                        // Convertir coordenadas de la imagen (0-1) a coordenadas del mundo
                        Vector3 rawPosition = new Vector3(
                            (0.5f - x) * transform.localScale.x,
                            (0.5f - y) * transform.localScale.y,
                            -1f
                        );

                        // Aplicar suavizado a la posición
                        Vector3 smoothedPosition = SmoothPosition(rawPosition, keypointName);

                        keypoints[keypointName].transform.localPosition = smoothedPosition;
                        keypoints[keypointName].SetActive(true);

                        // Debug para ver las coordenadas
                        Debug.Log($"Keypoint {keypointName}: confidence={confidence}, x={x}, y={y}, pos={smoothedPosition}");

                        // Agregar coordenadas 2D (usando las mismas coordenadas que para 3D)
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
                keypointIndex++; // Solo incrementamos para keypoints válidos
            }

            UpdateConnections();

            // 7. Liberar los tensores cuando ya no sean necesarios
            tensorEntrada.Dispose();
            readableTensor.Dispose();

        }
        catch (System.Exception e)
        {
            Debug.LogError("Error al generar el tensor Sentis: " + e.Message);
        }
    }
}
