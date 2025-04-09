using UnityEngine;

namespace LowEndGames.EQS
{
    [CreateAssetMenu(menuName = "EQS Query Settings")]
    public class EQS_GlobalSettings : ScriptableObject
    {
        public float NavMeshSampleRadius = .1f;
    }
}