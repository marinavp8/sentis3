using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Logger : MonoBehaviour
{
    public static void Log(string message, GameObject origin = null)
    {
#if UNITY_EDITOR
        if (origin != null)
        {
            Debug.Log(message, origin);
        }
        else
        {
            Debug.Log(message);
        }
#endif
    }
    public static void LogWarning(string message, GameObject origin = null)
    {
#if UNITY_EDITOR
        if (origin != null)
        {
            Debug.LogWarning(message, origin);
        }
        else
        {
            Debug.LogWarning(message);
        }
#endif
    }
    public static void LogError(string message, GameObject origin = null)
    {
#if UNITY_EDITOR
        if (origin != null)
        {
            Debug.LogError(message, origin);
        }
        else
        {
            Debug.LogError(message);
        }
#endif
    }
}
