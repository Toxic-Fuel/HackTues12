using System;
using UnityEngine;

namespace BabaResidence
{
    [Serializable]
    public class BabaResidenceActionDefinition
    {
        public BabaResidenceActionType actionType;

        [Header("Turn Costs")]
        [Min(0)] public int actionPointsCost = 1;
        [Min(0)] public int remainingTurnsCost;

        [Header("Material Costs")]
        [Min(0)] public int woodCost;
        [Min(0)] public int stoneCost;

        [Header("Social Deltas")]
        public int trustDelta;
        public int knowledgeDelta;
        public int volunteerEnergyDelta;
        public int reputationDelta;
        public int budgetDelta;

        [Header("Project")]
        public int projectProgressDelta;
        public int localOwnershipDelta;

        [Header("Random Event")]
        [Range(0f, 1f)] public float randomEventChance = 0.15f;
    }
}