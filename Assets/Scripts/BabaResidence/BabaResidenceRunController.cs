using System;
using System.Collections.Generic;
using UnityEngine;

namespace BabaResidence
{
    public class BabaResidenceRunController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Turns turns;
        [SerializeField] private BabaResidenceVillageProfile villageProfile;
        [SerializeField] private BabaResidenceProjectSystem projectSystem;

        [Header("Run")]
        [SerializeField, Min(1)] private int residenceLengthDays = 30;
        [SerializeField] private bool autoInitializeOnStart = true;

        [Header("Starting Social Resources")]
        [SerializeField, Range(0, 100)] private int startTrust = 30;
        [SerializeField, Range(0, 100)] private int startKnowledge = 20;
        [SerializeField, Range(0, 100)] private int startVolunteerEnergy = 55;
        [SerializeField, Range(0, 100)] private int startReputation = 15;
        [SerializeField, Min(0)] private int startBudget = 12;

        [Header("Daily Drift")]
        [SerializeField] private int idleDayTrustPenalty = 2;
        [SerializeField] private int idleDayReputationPenalty = 1;
        [SerializeField] private int dailyBudgetCost = 1;
        [SerializeField] private int dailyEnergyRecovery = 4;

        [Header("Actions")]
        [SerializeField] private List<BabaResidenceActionDefinition> actionDefinitions = new List<BabaResidenceActionDefinition>();

        public int CurrentDay { get; private set; }
        public int Trust { get; private set; }
        public int CulturalKnowledge { get; private set; }
        public int VolunteerEnergy { get; private set; }
        public int Reputation { get; private set; }
        public int Budget { get; private set; }
        public bool IsResidenceFinished { get; private set; }

        public event Action<BabaResidenceRunController> StatsChanged;
        public event Action<BabaResidenceRunController> DayAdvanced;
        public event Action<BabaResidenceRunController> ResidenceFinished;

        private int _actionsExecutedThisDay;

        private void Start()
        {
            if (turns == null)
            {
                turns = FindAnyObjectByType<Turns>();
            }

            if (villageProfile == null)
            {
                villageProfile = FindAnyObjectByType<BabaResidenceVillageProfile>();
            }

            if (projectSystem == null)
            {
                projectSystem = FindAnyObjectByType<BabaResidenceProjectSystem>();
            }

            if (autoInitializeOnStart)
            {
                InitializeRun();
            }
        }

        private void OnEnable()
        {
            if (turns == null)
            {
                turns = FindAnyObjectByType<Turns>();
            }

            if (turns != null)
            {
                turns.TurnEnded -= OnTurnEnded;
                turns.TurnEnded += OnTurnEnded;
            }
        }

        private void OnDisable()
        {
            if (turns != null)
            {
                turns.TurnEnded -= OnTurnEnded;
            }
        }

        public void InitializeRun()
        {
            EnsureActionDefinitions();

            if (villageProfile != null)
            {
                villageProfile.RegenerateProfile();
            }

            CurrentDay = 1;
            IsResidenceFinished = false;
            _actionsExecutedThisDay = 0;

            Trust = Mathf.Clamp(startTrust, 0, 100);
            CulturalKnowledge = Mathf.Clamp(startKnowledge, 0, 100);
            VolunteerEnergy = Mathf.Clamp(startVolunteerEnergy, 0, 100);
            Reputation = Mathf.Clamp(startReputation, 0, 100);
            Budget = Mathf.Max(0, startBudget);

            if (projectSystem != null && projectSystem.CurrentFocus == BabaResidenceProjectFocus.None)
            {
                BabaResidenceProjectFocus focus = PickFocusByNeed();
                projectSystem.StartProject(focus);
            }

            StatsChanged?.Invoke(this);
        }

        public bool TryExecuteAction(BabaResidenceActionType actionType)
        {
            if (IsResidenceFinished)
            {
                return false;
            }

            if (turns == null)
            {
                turns = FindAnyObjectByType<Turns>();
                if (turns == null)
                {
                    Debug.LogError("BabaResidenceRunController: Turns reference is missing.", this);
                    return false;
                }
            }

            if (turns.State != Turns.TurnState.PlayerTurn || !turns.CanTakeAction)
            {
                return false;
            }

            BabaResidenceActionDefinition definition = GetActionDefinition(actionType);
            if (definition == null)
            {
                Debug.LogWarning($"BabaResidenceRunController: Missing action definition for {actionType}.", this);
                return false;
            }

            if (definition.actionPointsCost > turns.ActionsRemaining)
            {
                return false;
            }

            int[] resourceCost = BuildResourceCost(definition);
            if (!turns.CanAffordResources(resourceCost))
            {
                return false;
            }

            if (definition.remainingTurnsCost > 0 && !turns.CanAffordTurns(definition.remainingTurnsCost))
            {
                return false;
            }

            if (!turns.TrySpendResources(resourceCost))
            {
                return false;
            }

            if (definition.remainingTurnsCost > 0 && !turns.TrySpendTurns(definition.remainingTurnsCost))
            {
                return false;
            }

            if (!turns.TrySpendAction(definition.actionPointsCost))
            {
                return false;
            }

            ApplyDeltas(definition);
            ApplyRandomEvent(definition);
            _actionsExecutedThisDay++;

            if (projectSystem != null)
            {
                projectSystem.AddContribution(
                    definition.projectProgressDelta,
                    definition.localOwnershipDelta,
                    Trust,
                    CulturalKnowledge,
                    Reputation);
            }

            StatsChanged?.Invoke(this);
            return true;
        }

        public int GetVitalityIndex()
        {
            float projectScore = projectSystem == null ? 0f : projectSystem.ProgressNormalized * 100f;
            float vitality =
                0.35f * Trust +
                0.25f * CulturalKnowledge +
                0.20f * Reputation +
                0.20f * projectScore;

            return Mathf.RoundToInt(Mathf.Clamp(vitality, 0f, 100f));
        }

        private void OnTurnEnded(Turns _)
        {
            if (IsResidenceFinished)
            {
                return;
            }

            ResolveDailyDrift();

            CurrentDay++;
            DayAdvanced?.Invoke(this);
            StatsChanged?.Invoke(this);

            if (CurrentDay > residenceLengthDays)
            {
                IsResidenceFinished = true;
                ResidenceFinished?.Invoke(this);
            }
        }

        private void ResolveDailyDrift()
        {
            if (_actionsExecutedThisDay == 0)
            {
                Trust = Mathf.Clamp(Trust - Mathf.Abs(idleDayTrustPenalty), 0, 100);
                Reputation = Mathf.Clamp(Reputation - Mathf.Abs(idleDayReputationPenalty), 0, 100);
            }

            Budget = Mathf.Max(0, Budget - Mathf.Abs(dailyBudgetCost));
            VolunteerEnergy = Mathf.Clamp(VolunteerEnergy + Mathf.Abs(dailyEnergyRecovery), 0, 100);

            _actionsExecutedThisDay = 0;
        }

        private void ApplyDeltas(BabaResidenceActionDefinition definition)
        {
            Trust = Mathf.Clamp(Trust + definition.trustDelta, 0, 100);
            CulturalKnowledge = Mathf.Clamp(CulturalKnowledge + definition.knowledgeDelta, 0, 100);
            VolunteerEnergy = Mathf.Clamp(VolunteerEnergy + definition.volunteerEnergyDelta, 0, 100);
            Reputation = Mathf.Clamp(Reputation + definition.reputationDelta, 0, 100);
            Budget = Mathf.Max(0, Budget + definition.budgetDelta);
        }

        private void ApplyRandomEvent(BabaResidenceActionDefinition definition)
        {
            float chance = Mathf.Clamp01(definition.randomEventChance);
            if (chance <= 0f || UnityEngine.Random.value > chance)
            {
                return;
            }

            int socialMomentum = Trust + Reputation;
            bool positiveEvent = socialMomentum >= 90 || UnityEngine.Random.value > 0.45f;

            if (positiveEvent)
            {
                Budget = Mathf.Max(0, Budget + 2);
                Reputation = Mathf.Clamp(Reputation + 2, 0, 100);
                VolunteerEnergy = Mathf.Clamp(VolunteerEnergy + 1, 0, 100);
                return;
            }

            Budget = Mathf.Max(0, Budget - 2);
            Reputation = Mathf.Clamp(Reputation - 2, 0, 100);
            VolunteerEnergy = Mathf.Clamp(VolunteerEnergy - 2, 0, 100);
        }

        private int[] BuildResourceCost(BabaResidenceActionDefinition definition)
        {
            int resourceCount = 3;
            if (turns != null && turns.CurrentResources != null)
            {
                resourceCount = Mathf.Max(3, turns.CurrentResources.Length);
            }

            var cost = new int[resourceCount];
            cost[(int)ResourceType.Wood] = Mathf.Max(0, definition.woodCost);
            cost[(int)ResourceType.Stone] = Mathf.Max(0, definition.stoneCost);
            return cost;
        }

        private BabaResidenceActionDefinition GetActionDefinition(BabaResidenceActionType actionType)
        {
            for (int i = 0; i < actionDefinitions.Count; i++)
            {
                if (actionDefinitions[i] != null && actionDefinitions[i].actionType == actionType)
                {
                    return actionDefinitions[i];
                }
            }

            return null;
        }

        private BabaResidenceProjectFocus PickFocusByNeed()
        {
            if (villageProfile == null)
            {
                return BabaResidenceProjectFocus.PublicSpace;
            }

            int publicNeed = villageProfile.PublicSpaceNeed;
            int productNeed = villageProfile.LocalProductNeed;
            int eventNeed = villageProfile.SocialEventNeed;

            if (publicNeed >= productNeed && publicNeed >= eventNeed)
            {
                return BabaResidenceProjectFocus.PublicSpace;
            }

            if (productNeed >= publicNeed && productNeed >= eventNeed)
            {
                return BabaResidenceProjectFocus.LocalProduct;
            }

            return BabaResidenceProjectFocus.SocialEvent;
        }

        private void EnsureActionDefinitions()
        {
            if (actionDefinitions != null && actionDefinitions.Count > 0)
            {
                return;
            }

            actionDefinitions = new List<BabaResidenceActionDefinition>
            {
                new BabaResidenceActionDefinition
                {
                    actionType = BabaResidenceActionType.CommunityTalk,
                    trustDelta = 7,
                    knowledgeDelta = 3,
                    volunteerEnergyDelta = -4,
                    reputationDelta = 2,
                    projectProgressDelta = 4,
                    localOwnershipDelta = 4,
                    randomEventChance = 0.08f
                },
                new BabaResidenceActionDefinition
                {
                    actionType = BabaResidenceActionType.HelpWithChores,
                    trustDelta = 8,
                    knowledgeDelta = 1,
                    volunteerEnergyDelta = -7,
                    reputationDelta = 1,
                    woodCost = 1,
                    stoneCost = 1,
                    projectProgressDelta = 6,
                    localOwnershipDelta = 6,
                    randomEventChance = 0.12f
                },
                new BabaResidenceActionDefinition
                {
                    actionType = BabaResidenceActionType.FolkloreInterview,
                    trustDelta = 4,
                    knowledgeDelta = 9,
                    volunteerEnergyDelta = -5,
                    reputationDelta = 2,
                    projectProgressDelta = 5,
                    localOwnershipDelta = 3,
                    randomEventChance = 0.10f
                },
                new BabaResidenceActionDefinition
                {
                    actionType = BabaResidenceActionType.VolunteerCampaign,
                    trustDelta = 6,
                    knowledgeDelta = 2,
                    volunteerEnergyDelta = -10,
                    reputationDelta = 5,
                    woodCost = 2,
                    stoneCost = 2,
                    budgetDelta = -1,
                    projectProgressDelta = 10,
                    localOwnershipDelta = 8,
                    randomEventChance = 0.22f
                },
                new BabaResidenceActionDefinition
                {
                    actionType = BabaResidenceActionType.Workshop,
                    trustDelta = 5,
                    knowledgeDelta = 6,
                    volunteerEnergyDelta = -6,
                    reputationDelta = 3,
                    woodCost = 1,
                    budgetDelta = -1,
                    projectProgressDelta = 9,
                    localOwnershipDelta = 7,
                    randomEventChance = 0.16f
                },
                new BabaResidenceActionDefinition
                {
                    actionType = BabaResidenceActionType.PartnerMeeting,
                    trustDelta = -1,
                    knowledgeDelta = 0,
                    volunteerEnergyDelta = -3,
                    reputationDelta = 4,
                    budgetDelta = 4,
                    projectProgressDelta = 7,
                    localOwnershipDelta = 2,
                    randomEventChance = 0.25f
                },
                new BabaResidenceActionDefinition
                {
                    actionType = BabaResidenceActionType.Rest,
                    trustDelta = 0,
                    knowledgeDelta = 0,
                    volunteerEnergyDelta = 12,
                    reputationDelta = -1,
                    budgetDelta = -1,
                    projectProgressDelta = 0,
                    localOwnershipDelta = 0,
                    randomEventChance = 0.03f
                }
            };
        }
    }
}