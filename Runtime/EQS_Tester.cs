using System;
using LowEndGames.Utils;
using Unity.Collections;
using UnityEngine;

namespace LowEndGames.EQS
{
    [ExecuteInEditMode]
    public class EQS_Tester : MonoBehaviour
    {
        [SerializeField] private int m_gridSize = 16;
        [SerializeField] private float m_gridSpacing = 1f;
        [SerializeField] private Transform m_target;
        [SerializeField] private LayerMask m_obstacleMask;
        [SerializeField] [NavMeshArea] private int m_navMeshAreas;
        [SerializeField] private EnvironmentQuerySystem.QueryFlags m_flags;
        [SerializeField] private float m_maxDistance = 10f;
        [SerializeField] private QueryTypes m_queryType;
        [SerializeField] private Gradient m_scoreGradient;
        
        public enum QueryTypes
        {
            NearestCover,
            ShootingPosition
        }
        
        private EnvironmentQuerySystem.SamplePoint[] m_result;

        [ContextMenu("Test Query")]
        private void TestQuery()
        {
            Func<EnvironmentQuerySystem.SamplePoint, float> scoreFunction = m_queryType switch
            {
                QueryTypes.NearestCover => ScoreNearestCover,
                QueryTypes.ShootingPosition => ScoreShootingPosition,
                _ => throw new ArgumentOutOfRangeException()
            };

            EnvironmentQuerySystem.RunQuery(new EnvironmentQuerySystem.Query()
            {
                Flags = m_flags,
                Origin = transform.position,
                GridSize = m_gridSize,
                GridSpacing = m_gridSpacing,
                Target = m_target.position,
                ObstacleMask = m_obstacleMask,
                Callback = OnQueryComplete,
                NavMeshAreas = m_navMeshAreas,
                ScoreFunction = scoreFunction,
            });
        }

        private float ScoreNearestCover(EnvironmentQuerySystem.SamplePoint samplePoint)
        {
            if (!samplePoint.CanSeeTarget)
            {
                return 1f - samplePoint.DistanceToOrigin / m_maxDistance;
            }

            return 0f;
        }

        private float ScoreShootingPosition(EnvironmentQuerySystem.SamplePoint samplePoint)
        {
            if (samplePoint.CanSeeTarget)
            {
                return 1f - samplePoint.DistanceToOrigin / m_maxDistance;
            }

            return 0;
        }

        private void OnValidate()
        {
            TestQuery();
        }

        private void Update()
        {
            if (transform.hasChanged)
            {
                TestQuery();
                transform.hasChanged = false;
            }
        }

        private void OnQueryComplete(NativeArray<EnvironmentQuerySystem.SamplePoint> result)
        {
            m_result = result.ToArray();
        }

        private void OnDrawGizmos()
        {
            if (m_result == null) 
                return;
            
            foreach (var e in m_result)
            {
                if (!e.IsValid)
                    continue;
                
                Gizmos.color = e.Score > 0 
                    ? m_scoreGradient.Evaluate(e.Score)
                    : Color.red;

                if (e.Score <= 0)
                {
                    Gizmos.color = Color.red.WithA(0.25f);
                    Gizmos.DrawWireSphere(e.Point, .3f);
                }
                else
                {
                    Gizmos.DrawSphere(e.Point, .3f); 
                    UnityEditor.Handles.Label(e.Point + Vector3.up, $"{e.Score:F2}");    
                }
            }
        }
    }
}