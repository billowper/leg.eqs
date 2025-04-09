using System.Collections.Generic;
using UnityEngine;

namespace LowEndGames.EQS
{
    public static class EQS_Utils
    {
        // Offsets for 8-directional neighbors (including diagonals)
        private static readonly int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        private static readonly int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        
        public static IEnumerable<int> GetNeighborIndices(int index, int gridWidth, int gridHeight)
        {
            var x = index % gridWidth;
            var y = index / gridWidth;

            for (int i = 0; i < 8; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];

                if (nx >= 0 && nx < gridWidth && ny >= 0 && ny < gridHeight) 
                {
                    yield return ny * gridWidth + nx;
                }
            }
        }

        public static void SnapPositionToWorldGrid(ref Vector3 pos, float snapValue)
        {
            var snappedX = Mathf.Round(pos.x / snapValue) * snapValue;
            var snappedY = Mathf.Round(pos.y / snapValue) * snapValue;
            var snappedZ = Mathf.Round(pos.z / snapValue) * snapValue;

            pos = new Vector3(snappedX, snappedY, snappedZ);
        }
    }
}