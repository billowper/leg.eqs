using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Profiling;

namespace LowEndGames.EQS
{
    public delegate float PointScoreFunction(EnvironmentQuerySystem.Query query,
        int pointIndex,
        EnvironmentQuerySystem.SamplePoint samplePoint,
        NativeArray<EnvironmentQuerySystem.SamplePoint> points);
    
    public static class EnvironmentQuerySystem
    {
        public static void Update()
        {
            if (m_requestQueue.TryDequeue(out var request))
            {
                RunQuery(request);
            }
        }
        
        public static void AddQuery(Query query)
        {
            m_requestQueue.Enqueue(query);
        }

        public static void RunQuery(Query query)
        {
            Debug.Log($"[EnvironmentQuerySystem]: RunQuery - {query.Source.name}");
            
            Profiler.BeginSample("RunQuery");
            
            var totalPoints = query.GridSize * query.GridSize;
            var results = new NativeArray<SamplePoint>(totalPoints, Allocator.Temp);
    
            Execute(query, results);
            
            query.Callback(results);
            
            results.Dispose();
            
            Profiler.EndSample();
        }

        [Flags]
        public enum QueryFlags
        {
            DistanceToTarget = 1 << 0,
            DistanceToOrigin = 1 << 1,
            LineOfSightToOrigin = 1 << 2,
            LineOfSightToTarget = 1 << 3,
            OverlapCheck = 1 << 4,
        }
        
        public struct Query
        {
            public GameObject Source;
            public QueryFlags Flags;
            public Vector3 Origin;
            public Vector3 LineOfSightOrigin;
            public int GridSize;
            public float GridSpacing;
            public Vector3 Target;
            public LayerMask ObstacleMask;
            public Action<NativeArray<SamplePoint>> Callback;
            public int NavMeshAreas;
            public PointScoreFunction ScoreFunction;
            public Vector3 OverlapSize;
        }

        public struct SamplePoint
        {
            public bool IsValid;
            
            public Vector3 Point;
            public float DistanceToOrigin;
            public float DistanceToTarget;
            public float DistanceToObstacle;
            public bool CanSeeTarget;
            public bool InLineOfSight;
            public bool IsClear;
            public float Score;

            public override string ToString()
            {
                return $"Score :  {Score:F3}\n" +
                       $"DistToOrigin :  {DistanceToOrigin:F3}\n" +
                       $"DistToTarget :  {DistanceToTarget:F3}\n" +
                       $"DistToObstacle :  {DistanceToObstacle:F3}\n" +
                       $"CanSeeTarget :  {CanSeeTarget}\n" +
                       $"InLineOfSight :  {InLineOfSight}\n" +
                       $"IsClear :  {IsClear}";
            }
        }
        
        // ------------------------------------------------- private 
        
        private static readonly Collider[] m_overlaps = new Collider[32];
        private static readonly Queue<Query> m_requestQueue = new Queue<Query>();

        private static void Execute(Query query, NativeArray<SamplePoint> results)
        {
            var i = 0;
            var max = query.GridSize / 2;
            
            // sample nodes
            
            for (int x = -max; x < max; x++)
            {
                for (int y = -max; y < max; y++)
                {
                    var point = query.Origin + new Vector3(x * query.GridSpacing, 0, y * query.GridSpacing);
                    Profiler.BeginSample("CreateSample");
                    results[i] = CreateSample(query, point);
                    i++;
                    Profiler.EndSample();
                }
            }

            // score all valid nodes
            
            for (var index = 0; index < results.Length; index++)
            {
                var point = results[index];
                if (point.IsValid)
                {
                    point.Score = query.ScoreFunction(query, index, point, results);
                    results[index] = point;
                }
            }
        }

        private static SamplePoint CreateSample(Query query, Vector3 point)
        {
            var sample = new SamplePoint();
            
            var onNavMesh = NavMesh.SamplePosition(point, out var hit, 1.0f, query.NavMeshAreas);
            if (onNavMesh)
            {
                sample.Point = hit.position;
                
                // if we failed an overlap check, early out
                
                if (query.Flags.HasFlagFast(QueryFlags.OverlapCheck))
                {
                    var boxCenter = point + Vector3.up * (query.OverlapSize.y / 2f + 0.01f);
                    var boxSize = query.OverlapSize;

                    var overlapHits = Physics.OverlapBoxNonAlloc(boxCenter, boxSize * .5f, m_overlaps, Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                    if (overlapHits > 0)
                    {
                        return sample;
                    }
                }

                sample.IsClear = true;
                sample.IsValid = true;

                var losOffset = Vector3.up * .5f;
                var dirToTarget = query.Target - (point + losOffset);

                if (query.Flags.HasFlagFast(QueryFlags.DistanceToTarget))
                {
                    sample.DistanceToTarget = dirToTarget.magnitude;
                }

                if (query.Flags.HasFlagFast(QueryFlags.DistanceToOrigin))
                {
                    sample.DistanceToOrigin = Vector3.Distance(point, query.Origin);
                }

                if (query.Flags.HasFlagFast(QueryFlags.LineOfSightToOrigin))
                {
                    sample.InLineOfSight = !Physics.Linecast(point, query.Origin, query.ObstacleMask);
                }

                if (query.Flags.HasFlagFast(QueryFlags.LineOfSightToTarget))
                {
                    if (Physics.Raycast(point.WithY(query.LineOfSightOrigin.y),
                            dirToTarget.normalized,
                            out var obstacleHit,
                            dirToTarget.magnitude,
                            query.ObstacleMask,
                            QueryTriggerInteraction.Ignore))
                    {
                        sample.DistanceToObstacle = obstacleHit.distance;
                    }
                    else
                    {
                        sample.CanSeeTarget = true;
                    }
                }
            }
            
            return sample;
        }
    }

    public static class QueryFlagsExtensions
    {
        public static bool HasFlagFast(this EnvironmentQuerySystem.QueryFlags value, EnvironmentQuerySystem.QueryFlags flag)
        {
            return (value & flag) != 0;
        }
    }
}