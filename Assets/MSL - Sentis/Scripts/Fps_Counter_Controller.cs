using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

public class Fps_Counter_Controller
{
    private int frameCount = 0;
    private float deltaTime = 0.0f;
    private float fps = 0.0f;
    Text _fpsText;

    Stopwatch stopwatch;
    Text _inferenceTimeText;

    public Fps_Counter_Controller(/*Text fpsText, Text inferenceTimeText*/)
    {
        _fpsText = GameObject.Find("Fps Text").GetComponent<Text>();
        _inferenceTimeText = GameObject.Find("Inference Time Text").GetComponent<Text>();
    }

    public void Calcule_FPS()
    {
        frameCount++;
        deltaTime += Time.deltaTime;

        if (deltaTime >= 1.0f)
        {
            fps = frameCount / deltaTime;
            _fpsText.text = $"FPS: {fps}";
            frameCount = 0;
            deltaTime = 0.0f;
        }
    }

    public void StartWatch()
    {
        stopwatch = new Stopwatch();
        stopwatch.Start();
    }

    public void StopWatch()
    {
        stopwatch.Stop();
        long elapsedMs = stopwatch.ElapsedMilliseconds;
        _inferenceTimeText.text = $"Tiempo de inferencia: {elapsedMs} ms";
    }


}
