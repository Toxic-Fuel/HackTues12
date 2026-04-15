using UnityEngine;
using UnityEngine.InputSystem;

namespace BabaResidence
{
    public class BabaResidenceDebugActions : MonoBehaviour
    {
        [SerializeField] private BabaResidenceRunController runController;
        [Header("Input (New Input System)")]
        [SerializeField] private InputActionReference previousAction;
        [SerializeField] private InputActionReference nextAction;
        [SerializeField] private InputActionReference executeAction;

        [Header("Selection")]
        [SerializeField] private bool wrapSelection = true;
        [SerializeField] private bool logSelectionChanges = true;

        [SerializeField] private int selectedActionIndex;

        private static readonly BabaResidenceActionType[] ActionOrder =
        {
            BabaResidenceActionType.CommunityTalk,
            BabaResidenceActionType.HelpWithChores,
            BabaResidenceActionType.FolkloreInterview,
            BabaResidenceActionType.VolunteerCampaign,
            BabaResidenceActionType.Workshop,
            BabaResidenceActionType.PartnerMeeting,
            BabaResidenceActionType.Rest
        };

        public BabaResidenceActionType SelectedAction => ActionOrder[Mathf.Clamp(selectedActionIndex, 0, ActionOrder.Length - 1)];

        private void Awake()
        {
            if (runController == null)
            {
                runController = FindAnyObjectByType<BabaResidenceRunController>();
            }

            selectedActionIndex = Mathf.Clamp(selectedActionIndex, 0, ActionOrder.Length - 1);
        }

        private void OnEnable()
        {
            RegisterAction(previousAction, OnPreviousActionPerformed);
            RegisterAction(nextAction, OnNextActionPerformed);
            RegisterAction(executeAction, OnExecuteActionPerformed);
        }

        private void OnDisable()
        {
            UnregisterAction(previousAction, OnPreviousActionPerformed);
            UnregisterAction(nextAction, OnNextActionPerformed);
            UnregisterAction(executeAction, OnExecuteActionPerformed);
        }

        private void OnPreviousActionPerformed(InputAction.CallbackContext context)
        {
            if (!context.performed || InGameGenerationMenu.IsAnyMenuOpen)
            {
                return;
            }

            MoveSelection(-1);
        }

        private void OnNextActionPerformed(InputAction.CallbackContext context)
        {
            if (!context.performed || InGameGenerationMenu.IsAnyMenuOpen)
            {
                return;
            }

            MoveSelection(1);
        }

        private void OnExecuteActionPerformed(InputAction.CallbackContext context)
        {
            if (!context.performed || InGameGenerationMenu.IsAnyMenuOpen)
            {
                return;
            }

            if (runController == null)
            {
                runController = FindAnyObjectByType<BabaResidenceRunController>();
                if (runController == null)
                {
                    return;
                }
            }

            runController.TryExecuteAction(SelectedAction);
        }

        private void MoveSelection(int delta)
        {
            if (ActionOrder.Length == 0 || delta == 0)
            {
                return;
            }

            int nextIndex = selectedActionIndex + delta;

            if (wrapSelection)
            {
                if (nextIndex < 0)
                {
                    nextIndex = ActionOrder.Length - 1;
                }
                else if (nextIndex >= ActionOrder.Length)
                {
                    nextIndex = 0;
                }
            }
            else
            {
                nextIndex = Mathf.Clamp(nextIndex, 0, ActionOrder.Length - 1);
            }

            if (nextIndex == selectedActionIndex)
            {
                return;
            }

            selectedActionIndex = nextIndex;
            if (logSelectionChanges)
            {
                Debug.Log($"BabaResidenceDebugActions: Selected {SelectedAction}", this);
            }
        }

        private static void RegisterAction(InputActionReference actionReference, System.Action<InputAction.CallbackContext> callback)
        {
            if (actionReference == null || actionReference.action == null)
            {
                return;
            }

            actionReference.action.Enable();
            actionReference.action.performed -= callback;
            actionReference.action.performed += callback;
        }

        private static void UnregisterAction(InputActionReference actionReference, System.Action<InputAction.CallbackContext> callback)
        {
            if (actionReference == null || actionReference.action == null)
            {
                return;
            }

            actionReference.action.performed -= callback;
            actionReference.action.Disable();
        }
    }
}