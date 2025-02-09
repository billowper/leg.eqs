using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace LowEndGames.EQS
{
    public static class EnvironmentQuerySystem
    {
        [Flags]
        public enum QueryFlags
        {
            DistanceToTarget = 1 << 0,
            DistanceToOrigin = 1 << 1,
            LineOfSightToOrigin = 1 << 2,
            LineOfSightToTarget = 1 << 3,
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
            var distToTarget = Mathf.Infinity;
            var distToOrigin = Mathf.Infinity;
            var hasLineOfSight = false;
            var canSeeTarget = false;
            
            var isValid = NavMesh.SamplePosition(point, out var hit, 1.0f, query.NavMeshAreas);
            if (isValid)
            {
                point = hit.position;

                var dirToTarget = (query.Target + Vector3.up * .25f) - (point + Vector3.up * .25f);

                if (query.Flags.HasFlagFast(QueryFlags.DistanceToTarget))
                {
                    distToTarget = dirToTarget.magnitude;
                }

                if (query.Flags.HasFlagFast(QueryFlags.DistanceToOrigin))
                {
                    distToOrigin = Vector3.Distance(point, query.Origin);
                }

                if (query.Flags.HasFlagFast(QueryFlags.LineOfSightToOrigin))
                {
                    hasLineOfSight = !Physics.Linecast(point, query.Origin, query.ObstacleMask);
                }

                if (query.Flags.HasFlagFast(QueryFlags.LineOfSightToTarget))
                {
                    canSeeTarget = !Physics.Linecast(point + Vector3.up * .25f, query.Target + Vector3.up * .25f, query.ObstacleMask);
                }
            }

            var sample = new SamplePoint
            {
                IsValid = isValid,
                Point = point,
                DistanceToTarget = distToTarget,
                DistanceToOrigin = distToOrigin,
                CanSeeTarget = canSeeTarget,
                InLineOfSight = hasLineOfSight,
            };
            
            sample.Score = query.ScoreFunction(sample);
            
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
            public Func<SamplePoint, float> ScoreFunction;
        }

        public struct SamplePoint
        {
            public bool IsValid;
            public Vector3 Point;
            public float DistanceToOrigin;
            public float DistanceToTarget;
            public bool CanSeeTarget;
            public bool InLineOfSight;
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