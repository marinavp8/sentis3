using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MSL_Sentis
{
    public class MSL_Sentis_Manager : MonoBehaviour
    {
        Pose_Detection _poseDetection;
        
        
        [Header("Skeleton")]
        Pose_Skeleton _poseSkeleton;
        [SerializeField] Material keypointMaterial;
        [SerializeField] float keypointSize = 0.02f;
        [SerializeField] Skeletons_SO _skeletons_SO;
        [SerializeField] int _skeletonToDraw = 0;
        [SerializeField] Material _lineMaterial;

        void Start()
        {
            _poseSkeleton = new(keypointMaterial, keypointSize, _skeletons_SO._list[_skeletonToDraw], _lineMaterial);
            _poseDetection = new();
        }
    }
}