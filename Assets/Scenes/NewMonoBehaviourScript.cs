using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    Model sourceModel;
    public GameObject middleSphere = null;
    public Worker worker;
    public Dictionary<string, GameObject> keypoints;
    private float lastProcessTime = 0f;
    public Material keypointMaterial; // Material para las esferas
    public Color keypointColor = Color.red; // Color para los keypoints
    public float keypointSize = 0.05f; // Tamaño de los keypoints
    public Color lineColor = Color.green; // Color para las líneas
    private LineRenderer[] connections;

    // Definir las conexiones entre keypoints para formar el esqueleto
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

    // Start is called before the first frame update
    const string keypointNames = "leftAnkle,leftEar,leftElbow,leftEye,leftHip,leftKnee,leftShoulder,leftWrist,nose,rect,rightAnkle,rightEar,rightElbow,rightEye,rightHip,rightKnee,rightShoulder,rightWrist";

    private GameObject CreateKeypointSphere(string name)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.parent = this.transform;
        sphere.transform.localScale = new Vector3(keypointSize, keypointSize, keypointSize);
        var renderer = sphere.GetComponent<Renderer>();
        if (keypointMaterial != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material = keypointMaterial;
        }
        else
        {
            renderer.material = new Material(Shader.Find("Sprites/Default")); // Cambiamos a Sprites/Default para mejor visibilidad
        }
        renderer.material.color = keypointColor;
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
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = lineColor;
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

    // Variables para dibujo 2D
    public float pointSize2D = 10f;
    public Color pointColor2D = Color.yellow;
    private Dictionary<string, Vector2> keypoints2D = new Dictionary<string, Vector2>();
    private Dictionary<string, float> keypointConfidence = new Dictionary<string, float>();
    private Texture2D pointTexture;

    // Variables para ajuste de imagen
    public float brightness = 1.2f; // Ajuste de brillo, > 1 aumenta, < 1 disminuye
    public float contrast = 1.2f;   // Ajuste de contraste, > 1 aumenta, < 1 disminuye

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

    void Start()
    {
        // Asegurarnos de que el objeto está en la posición correcta
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        // Ajustar el tamaño del plano para que coincida con el aspecto de la cámara
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

        // Load the model
        var PATH = "Assets/StreamingAssets/singlepose-thunder.sentis";
        sourceModel = ModelLoader.Load(PATH);
        Debug.Log("sourceModel: " + sourceModel);

        worker = new Worker(sourceModel, BackendType.GPUCompute);
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length > 0)
        {
            // Create a new WebCamTexture using the first available webcam
            WebCamTexture webCamTexture = new WebCamTexture();

            // Get the Renderer component from the GameObject
            Renderer renderer = GetComponent<Renderer>();
            // If the GameObject has a Renderer component, assign the webcam texture to it
            if (renderer != null)
            {
                renderer.material.mainTexture = webCamTexture;
                webCamTexture.Play();
            }
        }
        else
        {
            Debug.LogWarning("No webcam devices found.");
        }

        // Crear textura para los puntos 2D
        pointTexture = new Texture2D(1, 1);
        pointTexture.SetPixel(0, 0, Color.white);
        pointTexture.Apply();
    }

    void OnGUI()
    {
        if (keypoints2D == null || keypoints2D.Count == 0) return;

        // Obtener el renderer y la textura de la webcam
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null || !(renderer.material.mainTexture is WebCamTexture)) return;
        WebCamTexture webCamTexture = (WebCamTexture)renderer.material.mainTexture;

        // Calcular el rectángulo donde se muestra la imagen de la cámara
        float aspectRatio = (float)webCamTexture.width / webCamTexture.height;
        float scaleHeight = Screen.height;
        float scaleWidth = scaleHeight * aspectRatio;
        float leftIndent = (Screen.width - scaleWidth) / 2;

        Rect cameraRect = new Rect(leftIndent, 0, scaleWidth, scaleHeight);

        // Configurar estilo para el texto
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.normal.textColor = Color.white;
        style.fontSize = 12;
        style.fontStyle = FontStyle.Bold;

        // Dibujar cada punto
        foreach (var kvp in keypoints2D)
        {
            if (!keypointConfidence.ContainsKey(kvp.Key) || keypointConfidence[kvp.Key] < 0.5f) continue;

            // Convertir coordenadas normalizadas a coordenadas de pantalla dentro del viewport de la cámara
            float screenX = leftIndent + (kvp.Value.x * scaleWidth);
            float screenY = kvp.Value.y * scaleHeight;

            // Dibujar el punto
            GUI.color = pointColor2D;
            GUI.DrawTexture(
                new Rect(screenX - pointSize2D / 2, screenY - pointSize2D / 2, pointSize2D, pointSize2D),
                pointTexture
            );

            // Dibujar el nombre del punto y su score
            string label = $"{kvp.Key} ({keypointConfidence[kvp.Key]:F2})";
            GUI.color = Color.black;
            GUI.Label(
                new Rect(screenX + pointSize2D / 2 + 1, screenY - pointSize2D / 2 + 1, 150, 20),
                label,
                style
            );
            GUI.color = Color.white;
            GUI.Label(
                new Rect(screenX + pointSize2D / 2, screenY - pointSize2D / 2, 150, 20),
                label,
                style
            );
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Only run inference every 0.1 seconds to avoid performance issues
        if (Time.time - lastProcessTime < 0.1f)
            return;

        lastProcessTime = Time.time;

        // Get the webcam texture from the renderer
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null || renderer.material.mainTexture == null || !(renderer.material.mainTexture is WebCamTexture))
            return;

        WebCamTexture webCamTexture = (WebCamTexture)renderer.material.mainTexture;

        // Convert a texture to a tensor
        // Create a Texture2D from the WebCamTexture
        Texture2D texture2D = new Texture2D(webCamTexture.width, webCamTexture.height);

        // Obtener los colores y ajustar brillo/contraste
        Color32[] originalColors = webCamTexture.GetPixels32();
        Color32[] adjustedColors = AdjustImageColors(originalColors);

        // Aplicar los colores ajustados
        texture2D.SetPixels32(adjustedColors);
        texture2D.Apply();

        // Resize the texture to 256x256 for the model input
        Texture2D resizedTexture = new Texture2D(256, 256);

        // Create a temporary RenderTexture for resizing
        RenderTexture rt = RenderTexture.GetTemporary(256, 256);
        Graphics.Blit(texture2D, rt);

        // Read the resized texture
        RenderTexture.active = rt;
        resizedTexture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        resizedTexture.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        // Replace the original texture with the resized one
        texture2D = resizedTexture;
        try
        {
            // 1. Obtener los datos de la textura como un array de colores
            Color32[] colores = texture2D.GetPixels32();
            Debug.Log($"Texture size: {texture2D.width}x{texture2D.height}, Colors length: {colores.Length}");

            // 2. Crear un array de enteros para los datos del tensor
            int[] datosTensor = new int[texture2D.width * texture2D.height * 3];

            // 3. Convertir los colores a enteros y llenar el array de datos del tensor
            int indiceTensor = 0;
            for (int y = 0; y < texture2D.height; y++)
            {
                for (int x = 0; x < texture2D.width; x++)
                {
                    Color32 color = colores[y * texture2D.width + x];
                    datosTensor[indiceTensor++] = color.r; // Rojo
                    datosTensor[indiceTensor++] = color.g; // Verde
                    datosTensor[indiceTensor++] = color.b; // Azul
                }
            }
            Debug.Log($"Tensor data size: {datosTensor.Length}");

            // 4. Crear la forma del tensor (1, 256, 256, 3)
            TensorShape formaTensor = new TensorShape(1, texture2D.height, texture2D.width, 3);
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
                    float x = outputData[baseIndex];
                    float y = outputData[baseIndex + 1];
                    float confidence = outputData[baseIndex + 2];

                    // Solo actualizamos si la confianza es suficiente
                    if (confidence > 0.5f)
                    {
                        // Convertir coordenadas de la imagen (0-1) a coordenadas del mundo
                        // Ajustamos las coordenadas para que coincidan con el tamaño del plano
                        Vector3 worldPosition = new Vector3(
                            (x - 0.5f) * transform.localScale.x, // Multiplicamos por la escala del plano
                            (0.5f - y) * transform.localScale.y, // Multiplicamos por la escala del plano
                            -0.1f // Ligeramente delante del plano
                        );

                        keypoints[keypointName].transform.localPosition = worldPosition;
                        keypoints[keypointName].SetActive(true);

                        // Debug para ver las coordenadas
                        Debug.Log($"Keypoint {keypointName}: x={x}, y={y}, pos={worldPosition}");

                        // Agregar coordenadas 2D
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
