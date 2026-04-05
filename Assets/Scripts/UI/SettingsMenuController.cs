using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using System.Collections.Generic;

[RequireComponent(typeof(UIDocument))]
public class SettingsMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UIDocument settingsDocument;

    [Header("Camera Sensitivity Targets")]
    [SerializeField] private CameraHoldMove cameraHoldMove;
    [SerializeField] private CameraLeftClick cameraLeftClick;
    [SerializeField] private CameraScroll cameraScroll;

    [Header("Disable While Settings Menu Is Open")]
    [SerializeField] private List<Component> componentsToDisableWhenMenuEnabled = new List<Component>();

    private SliderInt _sfxSlider;
    private SliderInt _musicSlider;
    private Slider _moveSensitivitySlider;
    private Slider _zoomSensitivitySlider;
    private DropdownField _texturesDropdown;
    private DropdownField _shadowsDropdown;
    private Button _closeButton;

    private readonly Dictionary<Component, bool> _previousEnabledStates = new Dictionary<Component, bool>();
    private bool _lastMenuVisibleState;
    private bool _isRestoringSettings;

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
        if (settingsDocument == null)
        {
            settingsDocument = GetComponent<UIDocument>();
        }
    }

    private void OnEnable()
    {
        CacheControls();
        CacheTargetControllers();
        LoadSavedSettingsToUI();
        RegisterCallbacks();
        UpdateSettings();

        _lastMenuVisibleState = IsMenuVisible();
        ApplyMenuEnabledState(_lastMenuVisibleState);
    }

    private void OnDisable()
    {
        SaveCurrentSettings();
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

        ApplySensitivityToCameraControllers();
        ApplyTextureQuality(TexturesQualityIndex);
        ApplyShadowQuality(ShadowsQualityIndex);

        if (!_isRestoringSettings)
        {
            SaveCurrentSettings();
        }
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

    private void LoadSavedSettingsToUI()
    {
        _isRestoringSettings = true;

        if (_sfxSlider != null)
        {
            int sfx = PlayerPrefs.GetInt(KeySfxVolume, DefaultSfxVolume);
            int clampedSfx = Mathf.Clamp(sfx, Mathf.RoundToInt(_sfxSlider.lowValue), Mathf.RoundToInt(_sfxSlider.highValue));
            _sfxSlider.SetValueWithoutNotify(clampedSfx);
        }

        if (_musicSlider != null)
        {
            int music = PlayerPrefs.GetInt(KeyMusicVolume, DefaultMusicVolume);
            int clampedMusic = Mathf.Clamp(music, Mathf.RoundToInt(_musicSlider.lowValue), Mathf.RoundToInt(_musicSlider.highValue));
            _musicSlider.SetValueWithoutNotify(clampedMusic);
        }

        if (_moveSensitivitySlider != null)
        {
            float move = PlayerPrefs.GetFloat(KeyMoveSensitivity, DefaultMoveSensitivity);
            float clampedMove = Mathf.Clamp(move, _moveSensitivitySlider.lowValue, _moveSensitivitySlider.highValue);
            _moveSensitivitySlider.SetValueWithoutNotify(clampedMove);
        }

        if (_zoomSensitivitySlider != null)
        {
            float zoom = PlayerPrefs.GetFloat(KeyZoomSensitivity, DefaultZoomSensitivity);
            float clampedZoom = Mathf.Clamp(zoom, _zoomSensitivitySlider.lowValue, _zoomSensitivitySlider.highValue);
            _zoomSensitivitySlider.SetValueWithoutNotify(clampedZoom);
        }

        if (_texturesDropdown != null)
        {
            int texturesIndex = PlayerPrefs.GetInt(KeyTexturesQualityIndex, DefaultTexturesQualityIndex);
            SetDropdownIndex(_texturesDropdown, texturesIndex);
        }

        if (_shadowsDropdown != null)
        {
            int shadowsIndex = PlayerPrefs.GetInt(KeyShadowsQualityIndex, DefaultShadowsQualityIndex);
            SetDropdownIndex(_shadowsDropdown, shadowsIndex);
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

        if (settingsDocument != null)
        {
            settingsDocument.enabled = false;
        }

        _lastMenuVisibleState = false;
        ApplyMenuEnabledState(false);
    }
    public void OnOpenSettingsMenu()
    {
        if (settingsDocument != null)
        {
            settingsDocument.enabled = true;
        }

        CacheControls();
        RegisterCallbacks();
        LoadSavedSettingsToUI();
        UpdateSettings();

        _lastMenuVisibleState = true;
        ApplyMenuEnabledState(true);
    }
}

