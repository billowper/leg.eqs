#if UNITY_EDITOR
using System;
using LowEndGames.Utils;
using Unity.Collections;
using UnityEditor;
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
        [SerializeField] private float m_pointSize = .15f;
        [SerializeField] private float m_losArrowLength = 1f;
        [SerializeField] private float m_gizmoRadius = 1f;
        [SerializeField] private LayerMask m_groundLayers;
        
        private EnvironmentQuerySystem.SamplePoint[] m_result;
        private EnvironmentQuerySystem.SamplePoint m_bestResult;
        private EnvironmentQuerySystem.Query m_query;

        [ContextMenu("Test Query")]
        private void TestQuery()
        {
            m_query = new EnvironmentQuerySystem.Query()
            {
                Source = gameObject,
                Flags = m_settings.Flags,
                Origin = transform.position,
                LineOfSightOrigin = transform.position + Vector3.up,
                GridSize = m_settings.GridSize,
                GridSpacing = m_settings.GridSpacing,
                Target = m_target.position + Vector3.up,
                ObstacleMask = m_settings.ObstacleMask,
                Callback = OnQueryComplete,
                NavMeshAreas = m_settings.NavMeshAreas,
                ScoreFunction = m_settings.GetScoreFunction(m_queryType),
                OverlapSize = m_settings.OverlapCheckSize
            };
            
            EnvironmentQuerySystem.RunQuery(m_query);
        }

        private void OnValidate()
        {
            TestQuery();
        }

        private void Update()
        {
            if (transform.hasChanged || m_target.hasChanged)
            {
                TestQuery();
                transform.hasChanged = false;
                m_target.hasChanged = false;
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
            
            if (SceneView.lastActiveSceneView == null) return; // check if a scene view exists

            // Get the mouse position in screen space relative to the Scene View.
            var mousePosition = Event.current.mousePosition;
            mousePosition.y = SceneView.lastActiveSceneView.camera.pixelHeight - mousePosition.y; // Invert y for screen space

            // Create a ray from the Scene View camera to the mouse position.
            var ray = SceneView.lastActiveSceneView.camera.ScreenPointToRay(mousePosition);

            // Perform the raycast
            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, m_groundLayers))
            {
                // Draw a gizmo at the hit point
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(hit.point, 0.2f);
            }
            
            foreach (var e in m_result)
            {
                if (!e.IsValid)
                    continue;
                
                var losOffset = Vector3.up * .5f;
                var dirToTarget = m_query.Target - (e.Point + losOffset);

                if (e.Score <= 0)
                {
                    Gizmos.color = Color.red.WithA(0.1f);
                    Gizmos.DrawWireSphere(e.Point, m_pointSize);
                }
                else
                {
                    Gizmos.color = e.Score > 0 
                        ? m_scoreGradient.Evaluate(e.Score)
                        : Color.red;
                    
                    Gizmos.DrawSphere(e.Point, m_pointSize); 
                    
                    var dir = -dirToTarget.XZ3D().normalized * m_losArrowLength;
                    var start = e.Point + dir;
                    
                    GizmosEx.DrawArrow(start, -dir, e.CanSeeTarget ? Color.green : Color.red);
                }
                
                if (Vector3.Distance(hit.point, e.Point) < m_gizmoRadius)
                {
                    var origin = e.Point.WithY(m_query.LineOfSightOrigin.y);

                    if (Physics.Raycast(origin,
                            dirToTarget.normalized,
                            out var obstacleHit,
                            dirToTarget.magnitude,
                            m_query.ObstacleMask,
                            QueryTriggerInteraction.Ignore))
                    {
                        Gizmos.color = Color.red;
                        Gizmos.DrawSphere(obstacleHit.point, .1f);
                    }
                    else
                    {
                        
                        Gizmos.color = Color.green;
                    }
                    
                    Gizmos.DrawRay(origin, dirToTarget);
                    
                    Handles.Label(e.Point + Vector3.up, $"{e}");
                }
            }
        }
    }
}
#endif