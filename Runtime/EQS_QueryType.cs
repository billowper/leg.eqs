using LowEndGames.Utils;
using Unity.Collections;
using UnityEngine;

namespace LowEndGames.EQS
{
    public abstract class EQS_QueryType : ScriptableObject
    {
        public int GridSize = 16;
        public float GridSpacing = 1f;
        public LayerMask ObstacleMask;
        [NavMeshArea]
        public int NavMeshAreas;
        public Vector3 OverlapCheckSize = new Vector3(.5f, 1.5f, .5f);
        public float MaxDistance = 10f;
        public float MaxDistanceFromCover = 1f;
        public AnimationCurve ResponseCurve = AnimationCurve.EaseInOut(0,0,1,1);

        public abstract float Score(EnvironmentQuerySystem.Query query, int pointIndex, EnvironmentQuerySystem.SamplePoint samplePoint, NativeArray<EnvironmentQuerySystem.SamplePoint> points);
    }
}