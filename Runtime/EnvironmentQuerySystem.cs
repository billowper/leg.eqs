using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace LowEndGames.EQS
{
    public static class EnvironmentQuerySystem
    {
        private static readonly Collider[] m_overlaps = new Collider[32];

        [Flags]
        public enum QueryFlags
        {
            DistanceToTarget = 1 << 0,
            DistanceToOrigin = 1 << 1,
            LineOfSightToOrigin = 1 << 2,
            LineOfSightToTarget = 1 << 3,
            OverlapCheck = 1 << 4,
        }
        
        public static void RunQuery(Query query)
        {
            var totalPoints = query.GridSize * query.GridSize;
            var results = new NativeArray<SamplePoint>(totalPoints, Allocator.Temp);
    
            Execute(query, results);
            
            query.Callback(results);
            
            results.Dispose();
        }

        private static void Execute(Query query, NativeArray<SamplePoint> results)
        {
            var i = 0;
            var max = query.GridSize / 2;
            
            for (int x = -max; x < max; x++)
            {
                for (int y = -max; y < max; y++)
                {
                    var point = query.Origin + new Vector3(x * query.GridSpacing, 0, y * query.GridSpacing);
                    results[i] = CreateSample(query, point);
                    i++;
                }
            }
        }

        private static SamplePoint CreateSample(Query query, Vector3 point)
        {
            var sample = new SamplePoint();
            
            var onNavMesh = NavMesh.SamplePosition(point, out var hit, 1.0f, query.NavMeshAreas);
            if (onNavMesh)
            {
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
                sample.Point = hit.position;

                var losOffset = Vector3.up * .5f;

                var dirToTarget = (query.Target + losOffset) - (point + losOffset);

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
                    if (Physics.Raycast(point + losOffset,
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
                
                sample.Score = query.ScoreFunction(query, sample);
            }
            
            return sample;
        }

        public struct Query
        {
            public QueryFlags Flags;
            public Vector3 Origin;
            public int GridSize;
            public float GridSpacing;
            public Vector3 Target;
            public LayerMask ObstacleMask;
            public Action<NativeArray<SamplePoint>> Callback;
            public int NavMeshAreas;
            public Func<Query, SamplePoint, float> ScoreFunction;
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