using System;
using LowEndGames.Utils;
using Unity.Collections;
using UnityEngine;

namespace LowEndGames.EQS
{
    [CreateAssetMenu(menuName = "EQS Query Settings")]
    public class EQS_QuerySettings : ScriptableObject
    {
        public int GridSize = 16;
        public float GridSpacing = 1f;
        public LayerMask ObstacleMask;
        [NavMeshArea]
        public int NavMeshAreas;
        public Vector3 OverlapCheckSize = new Vector3(.5f, 1.5f, .5f);
        public EnvironmentQuerySystem.QueryFlags Flags;
        public float MaxDistance = 10f;
        public float MaxDistanceFromCover = 3f;
        public AnimationCurve ResponseCurve = AnimationCurve.EaseInOut(0,0,1,1);
        
        public PointScoreFunction GetScoreFunction(QueryTypes queryType)
        {
            return queryType switch
            {
                QueryTypes.NearestCover => ScoreNearestCover,
                QueryTypes.ShootingPosition => ScoreShootingPosition,
                QueryTypes.RetreatPosition => ScoreRetreatPosition,
                _ => throw new ArgumentOutOfRangeException(nameof(queryType), queryType, null)
            };
        }
        
        private float ScoreNearestCover(EnvironmentQuerySystem.Query query, int pointIndex, EnvironmentQuerySystem.SamplePoint samplePoint, NativeArray<EnvironmentQuerySystem.SamplePoint> points)
        {
            // we want a point where the target cannot see us,
            // and then prefer close to an obstacle (e.g. near the pillar, not just in its shadow)
            
            if (samplePoint is { CanSeeTarget: false, IsClear: true })
            {
                var inverseDistToOrigin = 1f - Mathf.Clamp01(samplePoint.DistanceToOrigin / MaxDistance) * 2f;
                var inverseDistFromObstacle = 1f - Mathf.Clamp01(samplePoint.DistanceToObstacle / MaxDistanceFromCover);
                var combinedScore = inverseDistToOrigin + inverseDistFromObstacle;
                
                // if a neighbouring cell can see the target, we get a fat bonus

                foreach (var n in EQS_Utils.GetNeighborIndices(pointIndex, query.GridSize, query.GridSize))
                {
                    if (points[n].CanSeeTarget)
                    {
                        combinedScore += 1f;
                        break;
                    }
                }

                combinedScore = Mathf.Clamp01(combinedScore / 3f);
                
                return ResponseCurve.Evaluate(combinedScore) * inverseDistFromObstacle;
            }

            return 0f;
        }

        private float ScoreShootingPosition(EnvironmentQuerySystem.Query query, int pointIndex, EnvironmentQuerySystem.SamplePoint samplePoint, NativeArray<EnvironmentQuerySystem.SamplePoint> points)
        {
            if (samplePoint is { CanSeeTarget: true, IsClear: true })
            {
                return ResponseCurve.Evaluate(1f - Mathf.Clamp01(samplePoint.DistanceToOrigin / MaxDistance));
            }

            return 0;
        }

        private float ScoreRetreatPosition(EnvironmentQuerySystem.Query query, int pointIndex, EnvironmentQuerySystem.SamplePoint samplePoint, NativeArray<EnvironmentQuerySystem.SamplePoint> points)
        {
            // we want a point where the target cannot see us,
            // and then prefer close to an obstacle (e.g. near the pillar, not just in its shadow)
            
            if (samplePoint is { CanSeeTarget: false, IsClear: true })
            {
                var dirToPoint = query.Origin - samplePoint.Point;
                var dirToTarget = query.Origin - query.Target;
                var alignmentWithTargetView = Vector3.Dot(dirToPoint.normalized, dirToTarget.normalized);
                
                if (alignmentWithTargetView < 0f)
                {
                    var distFromTarget = Mathf.Clamp01(samplePoint.DistanceToTarget / MaxDistance);
                    var inverseDistToOrigin = (1f - Mathf.Clamp01(samplePoint.DistanceToOrigin / MaxDistance)) * 2;
                    var inverseDistFromObstacle = 1f - Mathf.Clamp01(samplePoint.DistanceToObstacle / MaxDistanceFromCover);
                    var alignment = Mathf.Abs(alignmentWithTargetView) * 2.5f;
                    var combinedScore = Mathf.Clamp01((distFromTarget + inverseDistToOrigin + inverseDistFromObstacle + alignment) / 4f);
                    
                    return ResponseCurve.Evaluate(combinedScore) * inverseDistFromObstacle;
                }
            }

            return 0f;
        }
    }
}