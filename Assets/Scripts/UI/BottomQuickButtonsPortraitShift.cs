using UnityEngine;
using UnityEngine.UI;

public class BottomQuickButtonsPortraitShift : MonoBehaviour
{
    [Header("Button References")]
    [SerializeField] private Button donationButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button endgameButton;

    [Header("Portrait Shift")]
    [SerializeField] private bool shiftButtonsOnPortrait = true;
    [SerializeField, Min(320f)] private float portraitMaxScreenWidthPixels = 1300f;
    [SerializeField, Min(0f)] private float portraitLeftShiftPixels = 10f;
    [SerializeField] private bool includeSafeAreaLeftShift = true;

    private RectTransform donationRect;
    private RectTransform settingsRect;
    private RectTransform endgameRect;

    private Vector2 donationBaseAnchor;
    private Vector2 settingsBaseAnchor;
    private Vector2 endgameBaseAnchor;

    private int cachedScreenWidth = -1;
    private int cachedScreenHeight = -1;

    private void Awake()
    {
        CacheRectTransforms();
        CacheBaseAnchors();
    }

    private void OnEnable()
    {
        CacheRectTransforms();
        CacheBaseAnchors();
        ApplyPortraitShift(force: true);
    }

    private void Update()
    {
        if (Screen.width == cachedScreenWidth && Screen.height == cachedScreenHeight)
        {
            return;
        }

        ApplyPortraitShift(force: false);
    }

    private void CacheRectTransforms()
    {
        // Try to use serialized references first
        if (donationButton != null && donationRect == null)
        {
            donationRect = donationButton.GetComponent<RectTransform>();
        }
        else if (donationButton == null)
        {
            // Try to find by name in siblings
            Transform parent = transform;
            if (parent != null)
            {
                foreach (Transform child in parent)
                {
                    if (child.name == "SummaryButton")
                    {
                        donationButton = child.GetComponent<Button>();
                        if (donationButton != null)
                        {
                            donationRect = child.GetComponent<RectTransform>();
                        }
                        break;
                    }
                }
            }
        }

        if (settingsButton != null && settingsRect == null)
        {
            settingsRect = settingsButton.GetComponent<RectTransform>();
        }
        else if (settingsButton == null)
        {
            // Try to find by name in siblings
            Transform parent = transform;
            if (parent != null)
            {
                foreach (Transform child in parent)
                {
                    if (child.name == "SettingsButton")
                    {
                        settingsButton = child.GetComponent<Button>();
                        if (settingsButton != null)
                        {
                            settingsRect = child.GetComponent<RectTransform>();
                        }
                        break;
                    }
                }
            }
        }

        if (endgameButton != null && endgameRect == null)
        {
            endgameRect = endgameButton.GetComponent<RectTransform>();
        }
        else if (endgameButton == null)
        {
            // Try to find by name in siblings
            Transform parent = transform;
            if (parent != null)
            {
                foreach (Transform child in parent)
                {
                    if (child.name == "Button")
                    {
                        endgameButton = child.GetComponent<Button>();
                        if (endgameButton != null)
                        {
                            endgameRect = child.GetComponent<RectTransform>();
                        }
                        break;
                    }
                }
            }
        }
    }

    private void CacheBaseAnchors()
    {
        if (donationRect != null)
        {
            donationBaseAnchor = donationRect.anchoredPosition;
        }

        if (settingsRect != null)
        {
            settingsBaseAnchor = settingsRect.anchoredPosition;
        }

        if (endgameRect != null)
        {
            endgameBaseAnchor = endgameRect.anchoredPosition;
        }
    }

    private void ApplyPortraitShift(bool force)
    {
        if (!force && Screen.width == cachedScreenWidth && Screen.height == cachedScreenHeight)
        {
            return;
        }

        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;

        bool isPortraitPhoneScreen = Screen.height > Screen.width && Screen.width <= portraitMaxScreenWidthPixels;

        float leftShift = 0f;
        if (shiftButtonsOnPortrait && isPortraitPhoneScreen)
        {
            leftShift = portraitLeftShiftPixels;
            if (includeSafeAreaLeftShift)
            {
                leftShift += Mathf.Max(0f, Screen.safeArea.xMin);
            }
        }

        if (donationRect != null)
        {
            donationRect.anchoredPosition = donationBaseAnchor + new Vector2(leftShift, 0f);
        }

        if (settingsRect != null)
        {
            settingsRect.anchoredPosition = settingsBaseAnchor + new Vector2(leftShift, 0f);
        }

        if (endgameRect != null)
        {
            endgameRect.anchoredPosition = endgameBaseAnchor + new Vector2(leftShift, 0f);
        }
    }
}
