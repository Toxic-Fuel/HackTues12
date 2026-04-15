using UnityEngine;

namespace BabaResidence
{
    public enum BabaResidenceProjectFocus
    {
        None,
        PublicSpace,
        LocalProduct,
        SocialEvent
    }

    public class BabaResidenceProjectSystem : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private BabaResidenceProjectFocus currentFocus = BabaResidenceProjectFocus.None;
        [SerializeField, Min(1)] private int requiredProgress = 100;
        [SerializeField, Min(1)] private int requiredOwnership = 45;

        [Header("State")]
        [SerializeField, Min(0)] private int currentProgress;
        [SerializeField, Range(0, 100)] private int localOwnership;

        [Header("References")]
        [SerializeField] private BabaResidenceVillageProfile villageProfile;

        public BabaResidenceProjectFocus CurrentFocus => currentFocus;
        public int CurrentProgress => currentProgress;
        public int LocalOwnership => localOwnership;

        public bool IsOperational =>
            currentFocus != BabaResidenceProjectFocus.None &&
            currentProgress >= requiredProgress &&
            localOwnership >= requiredOwnership;

        public float ProgressNormalized => requiredProgress <= 0
            ? 1f
            : Mathf.Clamp01(currentProgress / (float)requiredProgress);

        public void StartProject(BabaResidenceProjectFocus focus)
        {
            currentFocus = focus;
            currentProgress = 0;
            localOwnership = 0;
        }

        public void AddContribution(int baseProgress, int ownershipDelta, int trust, int knowledge, int reputation)
        {
            if (currentFocus == BabaResidenceProjectFocus.None)
            {
                return;
            }

            float socialQualityMultiplier = 1f
                                            + trust / 250f
                                            + knowledge / 300f
                                            + reputation / 350f;

            float needAlignmentMultiplier = 1f;
            if (villageProfile != null)
            {
                int need = villageProfile.GetNeedForFocus(currentFocus);
                needAlignmentMultiplier += need / 250f;
            }

            int progressDelta = Mathf.Max(0, Mathf.RoundToInt(baseProgress * socialQualityMultiplier * needAlignmentMultiplier));
            currentProgress = Mathf.Max(0, currentProgress + progressDelta);

            int safeOwnershipDelta = Mathf.Max(0, ownershipDelta + Mathf.RoundToInt((trust + knowledge) / 60f));
            localOwnership = Mathf.Clamp(localOwnership + safeOwnershipDelta, 0, 100);
        }

        public bool TryAutoStartProject(BabaResidenceProjectFocus preferredFocus)
        {
            if (currentFocus != BabaResidenceProjectFocus.None)
            {
                return false;
            }

            StartProject(preferredFocus);
            return true;
        }
    }
}