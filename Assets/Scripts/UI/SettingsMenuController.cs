using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.Audio;
using System.Collections;
using UnityEngine.Events;

[RequireComponent(typeof(UIDocument))]
public class SettingsMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument settingsDocument;

    [Header("Audio Mixers")]
    [SerializeField] private AudioMixer sfxMixer;
    [SerializeField] private string sfxVolumeParameter = "SFXVolume";
    [SerializeField] private AudioMixer musicMixer;
    [SerializeField] private string musicVolumeParameter = "MusicVolume";

    [Header("Button Events")]
    [SerializeField] private UnityEvent OnButtonClicked;

    [Header("Camera Sensitivity Targets")]
    [SerializeField] private CameraHoldMove cameraHoldMove;
    [SerializeField] private CameraLeftClick cameraLeftClick;
    [SerializeField] private CameraScroll cameraScroll;

    [Header("Disable While Settings Menu Is Open")]
    [SerializeField] private List<Component> componentsToDisableWhenMenuEnabled = new List<Component>();

    [Header("Input Actions")]
    [SerializeField] private InputActionReference closeMenuAction;

    private SliderInt _sfxSlider;
    private SliderInt _musicSlider;
    private Slider _moveSensitivitySlider;
    private Slider _zoomSensitivitySlider;
    private DropdownField _texturesDropdown;
    private DropdownField _shadowsDropdown;
    private Button _closeButton;

    private readonly Dictionary<Component, bool> _previousEnabledStates = new Dictionary<Component, bool>();
    private readonly HashSet<string> _missingMixerParametersWarned = new HashSet<string>();

    private bool _lastMenuVisibleState;
    private bool _isRestoringSettings;
    private bool _hasLoadedSettings;
    private bool _startupSettingsApplied;
    private bool _startupDeferredApplyDone;

    private const string KeySfxVolume = "Settings.SfxVolume";
    private const string KeyMusicVolume = "Settings.MusicVolume";
    private const string KeyMoveSensitivity = "Settings.MoveSensitivity";
    private const string KeyZoomSensitivity = "Settings.ZoomSensitivity";
    private const string KeyTexturesQualityIndex = "Settings.TexturesQualityIndex";
    private const string KeyShadowsQualityIndex = "Settings.ShadowsQualityIndex";

    private const int DefaultSfxVolume = 100;
    private const int DefaultMusicVolume = 100;
    private const float DefaultMoveSensitivity = 1f;
    private const float DefaultZoomSensitivity = 1f;
    private const int DefaultTexturesQualityIndex = 1;
    private const int DefaultShadowsQualityIndex = 2;

    public int SfxVolume { get; private set; }
    public int MusicVolume { get; private set; }
    public float MoveSensitivity { get; private set; }
    public float ZoomSensitivity { get; private set; }
    public int TexturesQualityIndex { get; private set; }
    public int ShadowsQualityIndex { get; private set; }

    private void Awake()
    {
        OnButtonClicked ??= new UnityEvent();
        if (settingsDocument == null)
        {
            settingsDocument = GetComponent<UIDocument>();
        }

        EnsureStartupSettingsApplied();
    }

    private void OnEnable()
    {
        RegisterInputActions();

        CacheTargetControllers();
        EnsureStartupSettingsApplied();
        ApplyCurrentSettings();

        CacheControls();
        LoadStateToUI();
        RegisterCallbacks();

        _lastMenuVisibleState = IsMenuVisible();
        ApplyMenuEnabledState(_lastMenuVisibleState);

        if (!_startupDeferredApplyDone)
        {
            StartCoroutine(ApplyStartupSettingsDeferred());
        }
    }

    private void OnDisable()
    {
        UnregisterInputActions();

        if (_hasLoadedSettings)
        {
            SaveCurrentSettings();
        }

        UnregisterCallbacks();
        ApplyMenuEnabledState(false);
    }

    private void Update()
    {
        bool isMenuVisible = IsMenuVisible();
        if (isMenuVisible == _lastMenuVisibleState)
        {
            return;
        }

        _lastMenuVisibleState = isMenuVisible;
        ApplyMenuEnabledState(isMenuVisible);
    }

    public void UpdateSettings()
    {
        if (!AreControlsReady())
        {
            return;
        }

        SfxVolume = _sfxSlider != null ? _sfxSlider.value : 0;
        MusicVolume = _musicSlider != null ? _musicSlider.value : 0;
        MoveSensitivity = _moveSensitivitySlider != null ? _moveSensitivitySlider.value : 0f;
        ZoomSensitivity = _zoomSensitivitySlider != null ? _zoomSensitivitySlider.value : 0f;

        TexturesQualityIndex = GetDropdownIndex(_texturesDropdown);
        ShadowsQualityIndex = GetDropdownIndex(_shadowsDropdown);

        ApplyMixerVolumes();
        ApplySensitivityToCameraControllers();
        ApplyTextureQuality(TexturesQualityIndex);
        ApplyShadowQuality(ShadowsQualityIndex);

        if (!_isRestoringSettings)
        {
            _hasLoadedSettings = true;
            SaveCurrentSettings();
        }
    }

    private void ApplyCurrentSettings()
    {
        ApplyMixerVolumes();
        ApplySensitivityToCameraControllers();
        ApplyTextureQuality(TexturesQualityIndex);
        ApplyShadowQuality(ShadowsQualityIndex);
    }

    private bool AreControlsReady()
    {
        return _sfxSlider != null
            && _musicSlider != null
            && _moveSensitivitySlider != null
            && _zoomSensitivitySlider != null
            && _texturesDropdown != null
            && _shadowsDropdown != null;
    }

    private void CacheTargetControllers()
    {
        if (cameraHoldMove == null)
        {
            cameraHoldMove = FindAnyObjectByType<CameraHoldMove>();
        }

        if (cameraLeftClick == null)
        {
            cameraLeftClick = FindAnyObjectByType<CameraLeftClick>();
        }

        if (cameraScroll == null)
        {
            cameraScroll = FindAnyObjectByType<CameraScroll>();
        }
    }

    private void ApplySensitivityToCameraControllers()
    {
        if (cameraHoldMove != null)
        {
            cameraHoldMove.settingsDragSensitivity = MoveSensitivity;
        }

        if (cameraLeftClick != null)
        {
            cameraLeftClick.settingsDragSensitivity = MoveSensitivity;
        }

        if (cameraScroll != null)
        {
            cameraScroll.settingsZoomSpeed = ZoomSensitivity;
        }
    }

    private bool IsMenuVisible()
    {
        return settingsDocument != null && settingsDocument.enabled;
    }

    private void ApplyMenuEnabledState(bool isMenuVisible)
    {
        if (isMenuVisible)
        {
            for (int i = 0; i < componentsToDisableWhenMenuEnabled.Count; i++)
            {
                Component component = componentsToDisableWhenMenuEnabled[i];
                if (component == null || component == settingsDocument || component == this)
                {
                    continue;
                }

                if (TryGetComponentEnabled(component, out bool currentEnabled))
                {
                    if (!_previousEnabledStates.ContainsKey(component))
                    {
                        _previousEnabledStates[component] = currentEnabled;
                    }

                    SetComponentEnabled(component, false);
                }
            }

            return;
        }

        if (_previousEnabledStates.Count == 0)
        {
            return;
        }

        foreach (var pair in _previousEnabledStates)
        {
            if (pair.Key != null)
            {
                SetComponentEnabled(pair.Key, pair.Value);
            }
        }

        _previousEnabledStates.Clear();
    }

    private static bool TryGetComponentEnabled(Component component, out bool enabled)
    {
        enabled = false;
        if (component == null)
        {
            return false;
        }

        var property = component.GetType().GetProperty("enabled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (property == null || property.PropertyType != typeof(bool) || !property.CanRead)
        {
            return false;
        }

        enabled = (bool)property.GetValue(component);
        return true;
    }

    private static void SetComponentEnabled(Component component, bool enabled)
    {
        if (component == null)
        {
            return;
        }

        var property = component.GetType().GetProperty("enabled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
        {
            property.SetValue(component, enabled);
        }
    }

    private void LoadSavedSettingsToState()
    {
        SfxVolume = Mathf.Max(0, PlayerPrefs.GetInt(KeySfxVolume, DefaultSfxVolume));
        MusicVolume = Mathf.Max(0, PlayerPrefs.GetInt(KeyMusicVolume, DefaultMusicVolume));
        MoveSensitivity = Mathf.Max(0f, PlayerPrefs.GetFloat(KeyMoveSensitivity, DefaultMoveSensitivity));
        ZoomSensitivity = Mathf.Max(0f, PlayerPrefs.GetFloat(KeyZoomSensitivity, DefaultZoomSensitivity));
        TexturesQualityIndex = Mathf.Max(0, PlayerPrefs.GetInt(KeyTexturesQualityIndex, DefaultTexturesQualityIndex));
        ShadowsQualityIndex = Mathf.Max(0, PlayerPrefs.GetInt(KeyShadowsQualityIndex, DefaultShadowsQualityIndex));
        _hasLoadedSettings = true;
    }

    private void EnsureStartupSettingsApplied()
    {
        if (_startupSettingsApplied)
        {
            return;
        }

        LoadSavedSettingsToState();
        ApplyMixerVolumes();
        _startupSettingsApplied = true;
    }

    private void LoadStateToUI()
    {
        if (!AreControlsReady())
        {
            return;
        }

        _isRestoringSettings = true;

        if (_sfxSlider != null)
        {
            int clampedSfx = Mathf.Clamp(SfxVolume, Mathf.RoundToInt(_sfxSlider.lowValue), Mathf.RoundToInt(_sfxSlider.highValue));
            _sfxSlider.SetValueWithoutNotify(clampedSfx);
        }

        if (_musicSlider != null)
        {
            int clampedMusic = Mathf.Clamp(MusicVolume, Mathf.RoundToInt(_musicSlider.lowValue), Mathf.RoundToInt(_musicSlider.highValue));
            _musicSlider.SetValueWithoutNotify(clampedMusic);
        }

        if (_moveSensitivitySlider != null)
        {
            float clampedMove = Mathf.Clamp(MoveSensitivity, _moveSensitivitySlider.lowValue, _moveSensitivitySlider.highValue);
            _moveSensitivitySlider.SetValueWithoutNotify(clampedMove);
        }

        if (_zoomSensitivitySlider != null)
        {
            float clampedZoom = Mathf.Clamp(ZoomSensitivity, _zoomSensitivitySlider.lowValue, _zoomSensitivitySlider.highValue);
            _zoomSensitivitySlider.SetValueWithoutNotify(clampedZoom);
        }

        if (_texturesDropdown != null)
        {
            SetDropdownIndex(_texturesDropdown, TexturesQualityIndex);
        }

        if (_shadowsDropdown != null)
        {
            SetDropdownIndex(_shadowsDropdown, ShadowsQualityIndex);
        }

        _isRestoringSettings = false;
    }

    private static void SetDropdownIndex(DropdownField dropdown, int index)
    {
        if (dropdown == null)
        {
            return;
        }

        int count = dropdown.choices != null ? dropdown.choices.Count : 0;
        if (count <= 0)
        {
            dropdown.index = 0;
            return;
        }

        int clamped = Mathf.Clamp(index, 0, count - 1);
        dropdown.index = clamped;
        dropdown.SetValueWithoutNotify(dropdown.choices[clamped]);
    }

    private void SaveCurrentSettings()
    {
        PlayerPrefs.SetInt(KeySfxVolume, SfxVolume);
        PlayerPrefs.SetInt(KeyMusicVolume, MusicVolume);
        PlayerPrefs.SetFloat(KeyMoveSensitivity, MoveSensitivity);
        PlayerPrefs.SetFloat(KeyZoomSensitivity, ZoomSensitivity);
        PlayerPrefs.SetInt(KeyTexturesQualityIndex, TexturesQualityIndex);
        PlayerPrefs.SetInt(KeyShadowsQualityIndex, ShadowsQualityIndex);
        PlayerPrefs.Save();
    }

    private void ApplyMixerVolumes()
    {
        ApplyMixerVolume(sfxMixer, sfxVolumeParameter, SfxVolume);
        ApplyMixerVolume(musicMixer, musicVolumeParameter, MusicVolume);
    }

    private void ApplyMixerVolume(AudioMixer mixer, string exposedParameter, int sliderValue)
    {
        if (mixer == null)
        {
            return;
        }

        // Convert 0..100 slider to dB for AudioMixer.SetFloat.
        float normalized = Mathf.Clamp01(sliderValue / 100f);
        float db = normalized <= 0.0001f ? -80f : Mathf.Log10(normalized) * 20f;

        // Try the configured parameter first, then common fallback names.
        if (TrySetMixerDb(mixer, exposedParameter, db))
        {
            return;
        }

        if (TrySetMixerDb(mixer, "MasterVolume", db))
        {
            return;
        }

        if (TrySetMixerDb(mixer, "Volume", db))
        {
            return;
        }

        string warningKey = $"{mixer.name}|{exposedParameter}";
        if (_missingMixerParametersWarned.Add(warningKey))
        {
            Debug.LogWarning(
                $"SettingsMenuController: Could not set volume on mixer '{mixer.name}'. Expose a float parameter and set its exact name in the inspector. Tried '{exposedParameter}', 'MasterVolume', 'Volume'.",
                this
            );
        }
    }

    private static bool TrySetMixerDb(AudioMixer mixer, string parameterName, float db)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        return mixer.SetFloat(parameterName, db);
    }

    private static int GetDropdownIndex(DropdownField dropdown)
    {
        if (dropdown == null)
        {
            return 0;
        }

        int index = dropdown.index;
        if (index >= 0)
        {
            return index;
        }

        if (dropdown.choices == null)
        {
            return 0;
        }

        int fallback = dropdown.choices.IndexOf(dropdown.value);
        return fallback >= 0 ? fallback : 0;
    }

    private static void ApplyTextureQuality(int index)
    {
        // 0=Low, 1=Medium, 2=High from UXML dropdown order.
        int textureLimit = index switch
        {
            0 => 2,
            1 => 1,
            2 => 0,
            _ => 1
        };

        QualitySettings.globalTextureMipmapLimit = textureLimit;
    }

    private static void ApplyShadowQuality(int index)
    {
        if (TryApplyUrpShadowQuality(index))
        {
            return;
        }

        // 0=Off, 1=Low, 2=High from UXML dropdown order.
        ShadowQuality shadowQuality = index switch
        {
            0 => ShadowQuality.Disable,
            1 => ShadowQuality.HardOnly,
            2 => ShadowQuality.All,
            _ => ShadowQuality.HardOnly
        };

        QualitySettings.shadows = shadowQuality;
    }

    private static bool TryApplyUrpShadowQuality(int index)
    {
        RenderPipelineAsset pipeline = QualitySettings.renderPipeline != null
            ? QualitySettings.renderPipeline
            : GraphicsSettings.currentRenderPipeline;

        if (pipeline == null)
        {
            return false;
        }

        System.Type type = pipeline.GetType();
        if (type.FullName != "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")
        {
            return false;
        }

        switch (index)
        {
            case 0: // Off
                SetBoolProperty(pipeline, "supportsMainLightShadows", false);
                SetBoolProperty(pipeline, "supportsAdditionalLightShadows", false);
                SetIntProperty(pipeline, "shadowCascadeCount", 1);
                QualitySettings.shadows = ShadowQuality.Disable;
                break;

            case 1: // Low
                SetBoolProperty(pipeline, "supportsMainLightShadows", true);
                SetBoolProperty(pipeline, "supportsAdditionalLightShadows", false);
                SetIntProperty(pipeline, "shadowCascadeCount", 1);
                QualitySettings.shadows = ShadowQuality.HardOnly;
                break;

            case 2: // High
                SetBoolProperty(pipeline, "supportsMainLightShadows", true);
                SetBoolProperty(pipeline, "supportsAdditionalLightShadows", true);
                SetIntProperty(pipeline, "shadowCascadeCount", 4);
                QualitySettings.shadows = ShadowQuality.All;
                break;

            default:
                SetBoolProperty(pipeline, "supportsMainLightShadows", true);
                SetBoolProperty(pipeline, "supportsAdditionalLightShadows", false);
                SetIntProperty(pipeline, "shadowCascadeCount", 1);
                QualitySettings.shadows = ShadowQuality.HardOnly;
                break;
        }

        return true;
    }

    private static void SetBoolProperty(object target, string propertyName, bool value)
    {
        if (target == null)
        {
            return;
        }

        var property = target.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
        {
            property.SetValue(target, value);
        }
    }

    private static void SetIntProperty(object target, string propertyName, int value)
    {
        if (target == null)
        {
            return;
        }

        var property = target.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (property != null && property.CanWrite && property.PropertyType == typeof(int))
        {
            property.SetValue(target, value);
        }
    }

    private void CacheControls()
    {
        if (settingsDocument == null)
        {
            return;
        }

        VisualElement root = settingsDocument.rootVisualElement;
        if (root == null)
        {
            return;
        }

        _sfxSlider = root.Q<SliderInt>("SFXSlider");
        _musicSlider = root.Q<SliderInt>("MusicSlider");
        _moveSensitivitySlider = root.Q<Slider>("MoveSensitivitySlider");
        _zoomSensitivitySlider = root.Q<Slider>("ZoomSensitivitySlider");
        _texturesDropdown = root.Q<DropdownField>("TexturesDropdown");
        _shadowsDropdown = root.Q<DropdownField>("ShadowsDropdown");
        _closeButton = root.Q<Button>("CloseButton");
    }

    private void RegisterCallbacks()
    {
        // Rebind every time to avoid stale UI Toolkit element references after UIDocument toggles.
        UnregisterCallbacks();

        _sfxSlider?.RegisterValueChangedCallback(OnSettingChanged);
        _musicSlider?.RegisterValueChangedCallback(OnSettingChanged);
        _moveSensitivitySlider?.RegisterValueChangedCallback(OnSettingChanged);
        _zoomSensitivitySlider?.RegisterValueChangedCallback(OnSettingChanged);
        _texturesDropdown?.RegisterValueChangedCallback(OnSettingChanged);
        _shadowsDropdown?.RegisterValueChangedCallback(OnSettingChanged);

        if (_closeButton != null)
        {
            _closeButton.clicked += OnCloseClicked;
        }
    }

    private void UnregisterCallbacks()
    {
        _sfxSlider?.UnregisterValueChangedCallback(OnSettingChanged);
        _musicSlider?.UnregisterValueChangedCallback(OnSettingChanged);
        _moveSensitivitySlider?.UnregisterValueChangedCallback(OnSettingChanged);
        _zoomSensitivitySlider?.UnregisterValueChangedCallback(OnSettingChanged);
        _texturesDropdown?.UnregisterValueChangedCallback(OnSettingChanged);
        _shadowsDropdown?.UnregisterValueChangedCallback(OnSettingChanged);

        if (_closeButton != null)
        {
            _closeButton.clicked -= OnCloseClicked;
        }

    }

    private void OnSettingChanged(ChangeEvent<int> evt)
    {
        UpdateSettings();
    }

    private void OnSettingChanged(ChangeEvent<float> evt)
    {
        UpdateSettings();
    }

    private void OnSettingChanged(ChangeEvent<string> evt)
    {
        UpdateSettings();
    }

    private void OnCloseClicked()
    {
        UpdateSettings();
        OnButtonClicked?.Invoke();

        if (settingsDocument != null)
        {
            settingsDocument.enabled = false;
        }

        _lastMenuVisibleState = false;
        ApplyMenuEnabledState(false);
    }

    private void RegisterInputActions()
    {
        if (closeMenuAction == null || closeMenuAction.action == null)
        {
            return;
        }

        closeMenuAction.action.performed -= OnCloseMenuPerformed;
        closeMenuAction.action.performed += OnCloseMenuPerformed;
        closeMenuAction.action.Enable();
    }

    private void UnregisterInputActions()
    {
        if (closeMenuAction == null || closeMenuAction.action == null)
        {
            return;
        }

        closeMenuAction.action.performed -= OnCloseMenuPerformed;
        closeMenuAction.action.Disable();
    }

    private void OnCloseMenuPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed || settingsDocument == null || !settingsDocument.enabled)
        {
            return;
        }

        OnCloseClicked();
    }

    public void OnOpenSettingsMenu()
    {
        if (settingsDocument != null)
        {
            settingsDocument.enabled = true;
        }

        CacheControls();
        RegisterCallbacks();
        LoadSavedSettingsToState();
        LoadStateToUI();
        ApplyCurrentSettings();
        UpdateSettings();

        _lastMenuVisibleState = true;
        ApplyMenuEnabledState(true);
    }

    private IEnumerator ApplyStartupSettingsDeferred()
    {
        _startupDeferredApplyDone = true;

        // Reapply after initial scene startup in case another script overwrote mixer values.
        yield return null;
        LoadSavedSettingsToState();
        ApplyMixerVolumes();

        yield return null;
        ApplyMixerVolumes();
    }
}

