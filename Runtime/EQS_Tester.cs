using System;
using LowEndGames.Utils;
using Unity.Collections;
using UnityEngine;

namespace LowEndGames.EQS
{
    [ExecuteInEditMode]
    public class EQS_Tester : MonoBehaviour
    {
        [SerializeField] private EQS_QuerySettings m_settings;
        [SerializeField] private Transform m_target;
        [SerializeField] private QueryTypes m_queryType;
        [SerializeField] private Gradient m_scoreGradient;
        
        private EnvironmentQuerySystem.SamplePoint[] m_result;
        private EnvironmentQuerySystem.SamplePoint m_bestResult;

        [ContextMenu("Test Query")]
        private void TestQuery()
        {
            EnvironmentQuerySystem.RunQuery(new EnvironmentQuerySystem.Query()
            {
                Source = gameObject,
                Flags = m_settings.Flags,
                Origin = transform.position,
                GridSize = m_settings.GridSize,
                GridSpacing = m_settings.GridSpacing,
                Target = m_target.position,
                ObstacleMask = m_settings.ObstacleMask,
                Callback = OnQueryComplete,
                NavMeshAreas = m_settings.NavMeshAreas,
                ScoreFunction = m_settings.GetScoreFunction(m_queryType),
                OverlapSize = m_settings.OverlapCheckSize
            });
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

            if (m_result.Length > 0)
            {
                Array.Sort(m_result, (a, b) => b.Score.CompareTo(a.Score));

                foreach (var s in m_result)
                {
                    if (s.IsValid)
                    {
                        m_bestResult = s;
                        break;
                    }
                }
            }
            else
            {
                m_bestResult = default;
            }
        }

        private void OnDrawGizmos()
        {
            if (m_result == null) 
                return;
            
            GizmosEx.DrawArrow(m_target.position, m_target.forward, Color.cyan);
            
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, m_bestResult.Point);
            
            foreach (var e in m_result)
            {
                if (!e.IsValid)
                    continue;

                if (e.Score <= 0)
                {
                    Gizmos.color = Color.red.WithA(0.1f);
                    Gizmos.DrawWireSphere(e.Point, .3f);
                }
                else
                {
                    Gizmos.color = e.Score > 0 
                        ? m_scoreGradient.Evaluate(e.Score)
                        : Color.red;
                    
                    Gizmos.DrawSphere(e.Point, .3f); 
                    UnityEditor.Handles.Label(e.Point + Vector3.up, $"{e.Score:F2}");    
                }
            }
        }
    }
}