using System;
using GridGeneration;
using UnityEngine;

namespace BabaResidence
{
    public class BabaResidenceVillageProfile : MonoBehaviour
    {
        [Header("Generation")]
        [SerializeField] private bool autoGenerateOnAwake = true;
        [SerializeField] private bool useGridMapSeed = true;
        [SerializeField] private int fallbackSeed = 2026;
        [SerializeField] private GridMap gridMap;

        [Header("Identity")]
        [SerializeField] private string villageName = "Unnamed Village";

        [Header("Needs (0-100)")]
        [SerializeField, Range(0, 100)] private int publicSpaceNeed = 60;
        [SerializeField, Range(0, 100)] private int localProductNeed = 60;
        [SerializeField, Range(0, 100)] private int socialEventNeed = 60;

        public string VillageName => villageName;
        public int PublicSpaceNeed => publicSpaceNeed;
        public int LocalProductNeed => localProductNeed;
        public int SocialEventNeed => socialEventNeed;

        private static readonly string[] NamePrefix =
        {
            "Novo", "Gorno", "Dolno", "Staro", "Beli", "Zeleni", "Kamen"
        };

        private static readonly string[] NameSuffix =
        {
            "Pole", "Bair", "Dol", "Izvor", "Most", "Cheresha", "Livad"
        };

        private void Awake()
        {
            if (autoGenerateOnAwake)
            {
                RegenerateProfile();
            }
        }

        public void RegenerateProfile()
        {
            if (gridMap == null)
            {
                gridMap = FindAnyObjectByType<GridMap>();
            }

            int seed = ResolveSeed();
            var rng = new System.Random(seed);

            villageName = BuildVillageName(rng);

            publicSpaceNeed = rng.Next(45, 91);
            localProductNeed = rng.Next(40, 91);
            socialEventNeed = rng.Next(35, 91);

            if (gridMap != null)
            {
                int obstaclePressure = Mathf.RoundToInt(Mathf.Clamp01(gridMap.ObstaclePercent) * 100f);
                int minePressure = Mathf.RoundToInt(Mathf.Clamp01(gridMap.MaxMineSourcePercent) * 100f);

                publicSpaceNeed = Mathf.Clamp(publicSpaceNeed + obstaclePressure / 3, 0, 100);
                localProductNeed = Mathf.Clamp(localProductNeed + minePressure / 2, 0, 100);

                int mapArea = Mathf.Max(1, gridMap.Width * gridMap.Height);
                int mapComplexity = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(mapArea) * 1.5f), 0, 30);
                socialEventNeed = Mathf.Clamp(socialEventNeed + mapComplexity / 3, 0, 100);
            }
        }

        public int GetNeedForFocus(BabaResidenceProjectFocus focus)
        {
            switch (focus)
            {
                case BabaResidenceProjectFocus.PublicSpace:
                    return publicSpaceNeed;
                case BabaResidenceProjectFocus.LocalProduct:
                    return localProductNeed;
                case BabaResidenceProjectFocus.SocialEvent:
                    return socialEventNeed;
                default:
                    return 0;
            }
        }

        private int ResolveSeed()
        {
            if (useGridMapSeed && gridMap != null)
            {
                return gridMap.seed;
            }

            return fallbackSeed;
        }

        private static string BuildVillageName(System.Random rng)
        {
            int prefix = rng.Next(0, NamePrefix.Length);
            int suffix = rng.Next(0, NameSuffix.Length);
            return NamePrefix[prefix] + " " + NameSuffix[suffix];
        }
    }
}