using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Skeletons_SO", menuName = "Scriptable Objects/Skeletons")]
public class Skeletons_SO: ScriptableObject
{
    public List<Skeleton> _list = new List<Skeleton>();
}

[Serializable]
public struct Skeleton
{
   public string name;
   public List<Bone> bones;
}

[Serializable]
public struct Bone
{
   public string start;
   public string end;
}