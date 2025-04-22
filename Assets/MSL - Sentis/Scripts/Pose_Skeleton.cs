using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MSL_Sentis
{
    public class Pose_Skeleton : MonoBehaviour
    {
        Material _keypointMaterial = null;
        float _keypointSize = 0f;
        Dictionary<string, GameObject> _keypoints;
        Skeleton _skeleton;
        LineRenderer[] connections;
        Material _lineMaterial = null;


        public Pose_Skeleton(Material keypointMaterial, float keypointSize, Skeleton skeleton, Material lineMaterial)
        {
            _keypointMaterial = keypointMaterial;
            _keypointSize = keypointSize;
            _skeleton = skeleton;
            _lineMaterial = lineMaterial;

            _keypoints = new Dictionary<string, GameObject>();

            List<string> keypointNames = new();
            foreach (Bone bone in _skeleton.bones)
            {
                if (!keypointNames.Contains(bone.start))
                    keypointNames.Add(bone.start);
                if (!keypointNames.Contains(bone.end))
                    keypointNames.Add(bone.end);
            }

            foreach (string keypointName in keypointNames)
            {
                    _keypoints[keypointName] = CreateKeypointSphere(keypointName);
            }

            CreateConnections();
        }

        private GameObject CreateKeypointSphere(string name)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.parent = this.transform;
            sphere.transform.localScale = new Vector3(_keypointSize, _keypointSize, _keypointSize);
            var renderer = sphere.GetComponent<Renderer>();
            renderer.material = _keypointMaterial;
            return sphere;
        }

        private void CreateConnections()
        {
            connections = new LineRenderer[_skeleton.bones.Count];
            for (int i = 0; i < _skeleton.bones.Count; i++)
            {
                GameObject lineObj = new GameObject($"Connection_{i}");
                lineObj.transform.parent = this.transform;
                LineRenderer line = lineObj.AddComponent<LineRenderer>();
                line.material = _lineMaterial;
                line.startWidth = line.endWidth = _keypointSize * 0.5f;
                line.positionCount = 2;
                connections[i] = line;
            }
        }

        public void UpdateConnections()
        {
            for (int i = 0; i < _skeleton.bones.Count; i++)
            {
                var bone = _skeleton.bones[i];
                var line = connections[i];

                if (_keypoints.TryGetValue(bone.start, out GameObject start) &&
                    _keypoints.TryGetValue(bone.end, out GameObject end))
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
    }
}