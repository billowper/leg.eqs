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
        public static EQS_GlobalSettings Settings { get; set; }
        
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

        public struct Query
        {
            public readonly GameObject Source;
            public readonly Vector3 Origin;
            public readonly Vector3 LineOfSightOrigin;
            public readonly int GridSize;
            public readonly float GridSpacing;
            public readonly Vector3 Target;
            public readonly Vector3 LineOfSightTarget;
            public readonly LayerMask ObstacleMask;
            public readonly Action<NativeArray<SamplePoint>> Callback;
            public readonly int NavMeshAreas;
            public readonly PointScoreFunction ScoreFunction;
            public readonly Vector3 OverlapSize;
            public readonly float MaxDistanceFromCover;

            public Query(GameObject source, Vector3 origin, Vector3 lineOfSightOrigin, int gridSize, float gridSpacing, Vector3 target, Vector3 lineOfSightTarget, LayerMask obstacleMask, Action<NativeArray<SamplePoint>> callback, int navMeshAreas, PointScoreFunction scoreFunction, Vector3 overlapSize, float maxDistanceFromCover)
            {
                Source = source;
                Origin = origin;
                LineOfSightOrigin = lineOfSightOrigin;
                GridSize = gridSize;
                GridSpacing = gridSpacing;
                Target = target;
                LineOfSightTarget = lineOfSightTarget;
                ObstacleMask = obstacleMask;
                Callback = callback;
                NavMeshAreas = navMeshAreas;
                ScoreFunction = scoreFunction;
                OverlapSize = overlapSize;
                MaxDistanceFromCover = maxDistanceFromCover;
            }
        }

        public struct SamplePoint
        {
            public bool IsValid;
            
            public Vector3 Point;
            public float DistanceToOrigin;
            public float DistanceToTarget;
            public float DistanceToObstacle;
            public bool InLineOfSight;
            public bool IsClear;
            public bool CanSeeTarget;
            public CoverTypes CoverType;
            public float Score;

            public override string ToString()
            {
                return $"Score :  {Score:F3}\n" +
                       $"DistToOrigin :  {DistanceToOrigin:F3}\n" +
                       $"DistToTarget :  {DistanceToTarget:F3}\n" +
                       $"DistToObstacle :  {DistanceToObstacle:F3}\n" +
                       $"InLineOfSight :  {InLineOfSight}\n" +
                       $"IsClear :  {IsClear}\n" +
                       $"CanSeeTarget :  {CanSeeTarget}\n" +
                       $"CoverType :  {CoverType}";
            }
        }
        
        // ------------------------------------------------- private 
        
        private static readonly Collider[] m_overlaps = new Collider[32];
        private static readonly Queue<Query> m_requestQueue = new Queue<Query>();

        private static void Execute(Query query, NativeArray<SamplePoint> results)
        {
            var i = 0;
            var max = query.GridSize / 2;
            
            // snap to world grid 

            var origin = query.Origin;
            
            EQS_Utils.SnapPositionToWorldGrid(ref origin, query.GridSpacing);
            
            // sample nodes
            
            for (int x = -max; x < max; x++)
            {
                for (int y = -max; y < max; y++)
                {
                    var point = origin + new Vector3(x * query.GridSpacing, 0, y * query.GridSpacing);
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
            var sample = new SamplePoint()
            {
                Point = point
            };
            
            var onNavMesh = NavMesh.SamplePosition(point, out var hit, Settings.NavMeshSampleRadius, query.NavMeshAreas);
            if (onNavMesh)
            {
                sample.Point = hit.position;
                
                // if we failed an overlap check, early out
                
                var boxCenter = sample.Point + Vector3.up * (query.OverlapSize.y / 2f + 0.01f);
                var boxSize = query.OverlapSize;

                var overlapHits = Physics.OverlapBoxNonAlloc(boxCenter, boxSize * .5f, m_overlaps, Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                if (overlapHits > 0)
                {
                    return sample;
                }

                sample.IsClear = true;
                sample.IsValid = true;

                var losOffset = Vector3.up * .1f;

                sample.DistanceToTarget = (query.Target - sample.Point).magnitude;
                sample.DistanceToOrigin = Vector3.Distance(sample.Point, query.Origin);
                sample.InLineOfSight = !Physics.Linecast(sample.Point + losOffset, query.Origin + losOffset, query.ObstacleMask);
                
                // ground-level obstacle distance
                {
                    var obsCheckOrigin = sample.Point + losOffset;
                    var losDir = (query.Target + losOffset) - obsCheckOrigin;
                    
                    if (Physics.Raycast(obsCheckOrigin,
                            losDir.normalized,
                            out var obstacleHit,
                            losDir.magnitude,
                            query.ObstacleMask,
                            QueryTriggerInteraction.Ignore))
                    {
                        sample.DistanceToObstacle = obstacleHit.distance;
                    }
                    else
                    {
                        sample.DistanceToObstacle = Mathf.Infinity;
                    }
                }
                
                // eye-level line of sight
                {
                    var losOrigin = sample.Point.WithY(query.LineOfSightOrigin.y);
                    var losDir = query.LineOfSightTarget - losOrigin;

                    if (!Physics.Raycast(losOrigin,
                            losDir.normalized,
                            out _,
                            losDir.magnitude,
                            query.ObstacleMask,
                            QueryTriggerInteraction.Ignore))
                    {
                        sample.CanSeeTarget = true;
                    }
                }

                if (sample.DistanceToObstacle <= query.MaxDistanceFromCover)
                {
                    sample.CoverType = sample.CanSeeTarget ? CoverTypes.Half : CoverTypes.Full;
                }
                else
                {
                    sample.CoverType = CoverTypes.None;
                }
            }
            
            return sample;
        }
    }

    public enum CoverTypes
    {
        None,
        Half,
        Full
    }
}