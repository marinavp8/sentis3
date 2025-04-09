using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UI;
using System.Runtime.Serialization;
using System;

[Serializable]
public class PoseKeypoint
{
    public float score;
    public float x;
    public float y;
}

[Serializable]
public class PoseRect
{
    public float height;
    public float width;
    public float x;
    public float y;
}

[Serializable]
public class PoseData
{
    public PoseKeypoint leftAnkle;
    public PoseKeypoint leftEar;
    public PoseKeypoint leftElbow;
    public PoseKeypoint leftEye;
    public PoseKeypoint leftHip;
    public PoseKeypoint leftKnee;
    public PoseKeypoint leftShoulder;
    public PoseKeypoint leftWrist;
    public PoseKeypoint nose;
    public PoseRect rect;
    public PoseKeypoint rightAnkle;
    public PoseKeypoint rightEar;
    public PoseKeypoint rightElbow;
    public PoseKeypoint rightEye;
    public PoseKeypoint rightHip;
    public PoseKeypoint rightKnee;
    public PoseKeypoint rightShoulder;
    public PoseKeypoint rightWrist;
    public float score;
    
}


public class NewMonoBehaviourScript : MonoBehaviour
{
    Model sourceModel;
    public GameObject middleSphere = null;
    public Worker worker;
    public Dictionary<string, GameObject> keypoints;
    private float lastProcessTime = 0f;
    // Start is called bef  ore the first frame update
    const string keypointNames = "leftAnkle,leftEar,leftElbow,leftEye,leftHip,leftKnee,leftShoulder,leftWrist,nose,rect,rightAnkle,rightEar,rightElbow,rightEye,rightHip,rightKnee,rightShoulder,rightWrist";
    Tensor<int> inputTensor;
    
    void Start()
    {
        var PATH = "Assets/StreamingAssets/singlepose-thunder.sentis";
        sourceModel = ModelLoader.Load(PATH);
        Debug.Log("sourceModel: " + sourceModel);


        

        worker = new Worker( sourceModel, BackendType.GPUCompute);


        keypoints = new Dictionary<string, GameObject>();
        string[] keypointNamesArray = keypointNames.Split(',');
        foreach (string keypoint in keypointNamesArray)
        {
            GameObject gameobject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gameobject.name = keypoint;
            gameobject.SetActive(false);
            gameobject.transform.parent = transform;
            Debug.Log("Middle sphere created");
            // Add a distinctive material color
            Renderer sphereRenderer = gameobject.GetComponent<Renderer>();
            // Make the sphere smaller
            gameobject.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
            if (sphereRenderer)
            {
                sphereRenderer.material.color = UnityEngine.Color.red; // Make it red to stand out
            }
            keypoints.Add(keypoint, gameobject);
        }



        WebCamDevice[] devices = WebCamTexture.devices;
        // for debugging purposes, prints available devices to the console
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log("Webcam available: " + devices[i].name);
        }
        // Check if there are any webcam devices available
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
                inputTensor = TextureConverter
                .ToTensor<int>(webCamTexture, width: 256, height: 256, channels: 3);
            }
        }
        else
        {
            Debug.LogWarning("No webcam devices found.");
        }
    }
    void PintarPuntos(PoseData pose, Texture2D texture)
    {
        if (pose == null)
            return;
        // Get the current texture from the renderer
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null || !(renderer.material.mainTexture is WebCamTexture webCamTexture))
            return;

        DrawKeypointIfValid(pose.leftAnkle, "leftAnkle");
        DrawKeypointIfValid(pose.leftEar, "leftEar");
        DrawKeypointIfValid(pose.leftElbow, "leftElbow");
        DrawKeypointIfValid(pose.leftEye, "leftEye");
        DrawKeypointIfValid(pose.leftHip, "leftHip");
        DrawKeypointIfValid(pose.leftKnee, "leftKnee");
        DrawKeypointIfValid(pose.leftShoulder, "leftShoulder");
        DrawKeypointIfValid(pose.leftWrist, "leftWrist");
        DrawKeypointIfValid(pose.nose, "nose");
        DrawKeypointIfValid(pose.rightAnkle, "rightAnkle");
        DrawKeypointIfValid(pose.rightEar, "rightEar");
        DrawKeypointIfValid(pose.rightElbow, "rightElbow");
        DrawKeypointIfValid(pose.rightEye, "rightEye");
        DrawKeypointIfValid(pose.rightHip, "rightHip");
        DrawKeypointIfValid(pose.rightKnee, "rightKnee");
        DrawKeypointIfValid(pose.rightShoulder, "rightShoulder");
        DrawKeypointIfValid(pose.rightWrist, "rightWrist");
    }
    void DrawKeypointIfValid(PoseKeypoint keypoint, string keypointName)
    {
        float confidenceThreshold = 0.0f;
        // Only draw keypoints with sufficient confidence (adjust threshold as needed)
        GameObject keypointObj = keypoints[keypointName];
        if (keypoint != null && keypoint.score > confidenceThreshold)
        {
            keypointObj.SetActive(true);
            Vector3 worldPos = new Vector3(
                (keypoint.x) * transform.localScale.x,
                (0.5f - keypoint.y) * transform.localScale.y,
                -1f); // Slightly in front of the texture

            keypointObj.transform.position = worldPos;
            // Debug.Log($"Created keypoint at ({keypoint.x}, {keypoint.y}) with confidence {keypoint.score}");
        }
        else
        {
            keypointObj.SetActive(false);
        }
    }
    public Texture2D CrearTexturaDesdeColor32(Color32[] colores, int ancho, int alto)
    {
        // 1. Crea una nueva textura 2D
        Texture2D textura = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        // 2. Establece los píxeles de la textura
        textura.SetPixels32(colores);
        // 3. Aplica los cambios a la textura
        textura.Apply();
        // 4. Devuelve la textura creada
        return textura;
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
        // Copy the pixels from the WebCamTexture to the Texture2D
        texture2D.SetPixels32(webCamTexture.GetPixels32());
        texture2D.Apply();
        
        // Resize the texture to 256x256 for the model input
        Texture2D resizedTexture = new Texture2D(256, 256);
        // Replace the original texture with the resized one
        texture2D = resizedTexture;
        try
        {

            
            TextureConverter.ToTensor(webCamTexture, inputTensor, new TextureTransform());
            // 1. Obtener los datos de la textura como un array de colores
           
            // Record the start time for performance monitoring
             System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
              // Execute the model with the input tensor
            worker.Schedule(inputTensor);
            // Calculate and log the execution time
     
            stopwatch.Stop();
            Debug.Log("Prediction completed in: " + stopwatch.ElapsedMilliseconds + " milliseconds");
     

            // // Reset the worker for the next frame
            // // Get the output tensor from the model
            Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
            var readableTensor = outputTensor.ReadbackAndClone();
            var outputData = readableTensor.AsReadOnlySpan();

            // 
            // // Log the raw tensor data for debugging
            string[] keypointNamesArray = keypointNames.Split(',');
            var j = 0;
            for (int i = 0; i < keypointNamesArray.Length; i++)
            {
                PoseKeypoint keypoint = new PoseKeypoint();
                keypoint.x = outputData[j];
                keypoint.y = outputData[j+1];
                keypoint.score = outputData[j+2];
                j += 3;
                DrawKeypointIfValid(keypoint, keypointNamesArray[i]);
            }
            // 7. Liberar el tensor cuando ya no sea necesario.
            inputTensor.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error al generar el tensor Sentis: " + e.Message);
        }
    }
}

