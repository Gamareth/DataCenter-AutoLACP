using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AutoLACP;

internal static class AutoLacpGui
{
    private const float FillInset = 4f;
    private const string FillObjectName = "AutoLACP Fill";
    private const string CreateLabel = "[Auto] Create LACP";
    private const string RemoveLabel = "[Auto] Remove LACP";

    private static readonly Color NormalFill = FromRgb(0x35, 0x42, 0x4D);
    private static readonly Color HighlightedFill = FromRgb(0x40, 0x52, 0x60);
    private static readonly Color PressedFill = FromRgb(0x2B, 0x35, 0x3D);
    private static readonly Color LabelColor = FromRgb(0xD7, 0xDE, 0xE3);

    private static bool _configuredLogged;
    private static bool _missingButtonsLogged;

    internal static void Apply(NetworkSwitchConfiguration configuration)
    {
        if (configuration is null || !AutoLacpCoordinator.IsAutomaticEnabled)
            return;

        var searchRoot = configuration.parentObjectForPortText?.parent;
        if (searchRoot is null)
            searchRoot = configuration.transform;

        var configuredButtons = ConfigureButtons(searchRoot);
        for (var level = 0;
             configuredButtons == 0 && level < 2 && searchRoot.parent is not null;
             level++)
        {
            searchRoot = searchRoot.parent;
            configuredButtons = ConfigureButtons(searchRoot);
        }

        if (configuredButtons > 0)
        {
            _missingButtonsLogged = false;
            if (!_configuredLogged)
            {
                _configuredLogged = true;
                AutoLacpLog.Info(
                    $"Configured {configuredButtons} native LACP GUI " +
                    $"{Pluralize(configuredButtons, "button")} for AutoLACP.");
            }
            return;
        }

        if (_missingButtonsLogged)
            return;

        _missingButtonsLogged = true;
        AutoLacpLog.Warning(
            "No native CreateLACP or RemoveLACP GUI button could be found. " +
            "Manual actions remain blocked by the Harmony safety patches.");
    }

    private static int ConfigureButtons(Transform searchRoot)
    {
        var configuredButtons = 0;
        foreach (var button in searchRoot.GetComponentsInChildren<ButtonExtended>(true))
        {
            if (!IsLacpButton(button))
                continue;

            Configure(button);
            configuredButtons++;
        }

        foreach (var button in searchRoot.GetComponentsInChildren<Button>(true))
        {
            if (!IsLacpButton(button))
                continue;

            Configure(button);
            configuredButtons++;
        }
        return configuredButtons;
    }

    internal static void Reset()
    {
        _configuredLogged = false;
        _missingButtonsLogged = false;
    }

    private static bool IsLacpButton(Button button)
    {
        if (button is null)
            return false;

        var click = button.onClick;
        if (click is not null)
        {
            for (var index = 0; index < click.GetPersistentEventCount(); index++)
            {
                var methodName = click.GetPersistentMethodName(index);
                if (methodName == nameof(NetworkSwitchConfiguration.CreateLACP)
                    || methodName == nameof(NetworkSwitchConfiguration.RemoveLACP))
                {
                    return true;
                }
            }
        }

        return HasLacpActionLabel(button);
    }

    private static bool IsLacpButton(ButtonExtended button)
    {
        if (button is null)
            return false;

        var click = button.onClick;
        if (click is not null)
        {
            for (var index = 0; index < click.GetPersistentEventCount(); index++)
            {
                var methodName = click.GetPersistentMethodName(index);
                if (methodName == nameof(NetworkSwitchConfiguration.CreateLACP)
                    || methodName == nameof(NetworkSwitchConfiguration.RemoveLACP))
                {
                    return true;
                }
            }
        }

        return HasLacpActionLabel(button);
    }

    private static void Configure(Button button)
    {
        ConfigureSelectable(button);
        ConfigureLabel(button);
    }

    private static void Configure(ButtonExtended button)
    {
        ConfigureSelectable(button);
        ConfigureLabel(button);
    }

    private static void ConfigureSelectable(Selectable button)
    {
        var background = GetOrCreateBackground(button);
        background.color = NormalFill;
        button.targetGraphic = background;

        button.interactable = true;
        button.transition = Selectable.Transition.ColorTint;

        var colors = button.colors;
        colors.normalColor = NormalFill;
        colors.highlightedColor = HighlightedFill;
        colors.pressedColor = PressedFill;
        colors.selectedColor = NormalFill;
        button.colors = colors;
    }

    private static Image GetOrCreateBackground(Selectable button)
    {
        var existing = button.transform.Find(FillObjectName)?.GetComponent<Image>();
        if (existing is not null)
            return existing;

        var fillObject = new GameObject(
            FillObjectName,
            new[]
            {
                Il2CppType.Of<RectTransform>(),
                Il2CppType.Of<Image>()
            });
        fillObject.transform.SetParent(button.transform, false);
        fillObject.transform.SetAsFirstSibling();

        var rect = fillObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(FillInset, FillInset);
        rect.offsetMax = new Vector2(-FillInset, -FillInset);

        var background = fillObject.GetComponent<Image>();
        background.raycastTarget = false;
        return background;
    }

    private static void ConfigureLabel(Component button)
    {
        var label = FindLabel(button);
        if (label is null)
            return;

        label.text = label.text?.Contains("Remove", StringComparison.OrdinalIgnoreCase) == true
            ? RemoveLabel
            : CreateLabel;
        label.color = LabelColor;
    }

    private static bool HasLacpActionLabel(Component button)
    {
        var text = FindLabel(button)?.text;
        return !string.IsNullOrWhiteSpace(text)
            && text.Contains("LACP", StringComparison.OrdinalIgnoreCase)
            && (text.Contains("Create", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }

    private static TextMeshProUGUI FindLabel(Component button)
    {
        foreach (var label in button.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (label is not null)
                return label;
        }
        return null;
    }

    private static Color FromRgb(byte red, byte green, byte blue) =>
        new(red / 255f, green / 255f, blue / 255f, 1f);

    private static string Pluralize(int count, string singular) =>
        count == 1 ? singular : singular + "s";
}
