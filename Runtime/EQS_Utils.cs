using System.Collections.Generic;

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
    }
}