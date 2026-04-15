using UnityEngine;

namespace BabaResidence
{
    public class BabaResidenceWinLoseBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Turns turns;
        [SerializeField] private BabaResidenceRunController runController;
        [SerializeField] private BabaResidenceProjectSystem projectSystem;
        [SerializeField] private WinCondition roadWinCondition;

        [Header("Success Thresholds")]
        [SerializeField, Range(0, 100)] private int minTrust = 45;
        [SerializeField, Range(0, 100)] private int minKnowledge = 35;
        [SerializeField, Range(0, 100)] private int minReputation = 30;
        [SerializeField, Range(0, 100)] private int minVitality = 55;
        [SerializeField] private bool requireOperationalProject = true;
        [SerializeField] private bool requireRoadConnectivity = true;

        [Header("Early Failure")]
        [SerializeField] private bool allowEarlyTrustCollapseLoss = true;
        [SerializeField, Range(0, 100)] private int trustCollapseThreshold = 5;

        private void OnEnable()
        {
            CacheReferences();

            if (runController != null)
            {
                runController.ResidenceFinished -= OnResidenceFinished;
                runController.ResidenceFinished += OnResidenceFinished;

                runController.StatsChanged -= OnStatsChanged;
                runController.StatsChanged += OnStatsChanged;
            }
        }

        private void OnDisable()
        {
            if (runController != null)
            {
                runController.ResidenceFinished -= OnResidenceFinished;
                runController.StatsChanged -= OnStatsChanged;
            }
        }

        private void CacheReferences()
        {
            if (turns == null)
            {
                turns = FindAnyObjectByType<Turns>();
            }

            if (runController == null)
            {
                runController = FindAnyObjectByType<BabaResidenceRunController>();
            }

            if (projectSystem == null)
            {
                projectSystem = FindAnyObjectByType<BabaResidenceProjectSystem>();
            }

            if (roadWinCondition == null)
            {
                roadWinCondition = FindAnyObjectByType<WinCondition>();
            }
        }

        private void OnResidenceFinished(BabaResidenceRunController controller)
        {
            if (turns == null || controller == null)
            {
                return;
            }

            if (turns.State == Turns.TurnState.Win || turns.State == Turns.TurnState.Lose)
            {
                return;
            }

            bool trustOk = controller.Trust >= minTrust;
            bool knowledgeOk = controller.CulturalKnowledge >= minKnowledge;
            bool reputationOk = controller.Reputation >= minReputation;
            bool vitalityOk = controller.GetVitalityIndex() >= minVitality;

            bool projectOk = !requireOperationalProject ||
                             (projectSystem != null && projectSystem.IsOperational);

            bool roadOk = !requireRoadConnectivity ||
                          (roadWinCondition != null && roadWinCondition.AllVillagesConnected);

            bool isSuccess = trustOk && knowledgeOk && reputationOk && vitalityOk && projectOk && roadOk;

            if (isSuccess)
            {
                turns.TriggerWin();
            }
            else
            {
                turns.SetLoseState();
            }
        }

        private void OnStatsChanged(BabaResidenceRunController controller)
        {
            if (!allowEarlyTrustCollapseLoss || controller == null || turns == null)
            {
                return;
            }

            if (turns.State == Turns.TurnState.Win || turns.State == Turns.TurnState.Lose)
            {
                return;
            }

            if (controller.Trust <= trustCollapseThreshold)
            {
                turns.SetLoseState();
            }
        }
    }
}