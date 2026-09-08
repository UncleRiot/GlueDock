namespace GlueDock.DriveUsageWidget;

public sealed class DriveUsageSettings
{
    public List<string> SelectedDrives { get; set; } =
        new();

    public bool SettingsOpacityOverrideEnabled { get; set; }

    public double SettingsOpacity { get; set; } =
        0.45;

    public bool SettingsBlurOverrideEnabled { get; set; }

    public double SettingsBlurRadius { get; set; } =
        45;

    public string RingColor { get; set; } =
        "#FFFFA500";

    public double RingScalePercent { get; set; } =
        100;

    public double RingThicknessPercent { get; set; } =
        100;

    public string RingFillMode { get; set; } =
        "Used";

    public double CompanionScalePercent { get; set; } =
        100;

    public bool DisableDefaultHoverEffect { get; set; }

    public void CopyFrom(
        DriveUsageSettings source)
    {
        DriveUsageSettings normalized =
            source.Clone();

        SelectedDrives =
            normalized.SelectedDrives;
        SettingsOpacityOverrideEnabled =
            normalized.SettingsOpacityOverrideEnabled;
        SettingsOpacity =
            normalized.SettingsOpacity;
        SettingsBlurOverrideEnabled =
            normalized.SettingsBlurOverrideEnabled;
        SettingsBlurRadius =
            normalized.SettingsBlurRadius;
        RingColor =
            normalized.RingColor;
        RingScalePercent =
            normalized.RingScalePercent;
        RingThicknessPercent =
            normalized.RingThicknessPercent;
        RingFillMode =
            normalized.RingFillMode;
        CompanionScalePercent =
            normalized.CompanionScalePercent;
        DisableDefaultHoverEffect =
            normalized.DisableDefaultHoverEffect;
    }

    public DriveUsageSettings Clone()
    {
        return new DriveUsageSettings
        {
            SelectedDrives =
                SelectedDrives
                    .Where(
                        drive =>
                            !string.IsNullOrWhiteSpace(
                                drive))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .Take(
                        4)
                    .ToList(),
            SettingsOpacityOverrideEnabled =
                SettingsOpacityOverrideEnabled,
            SettingsOpacity =
                Math.Clamp(
                    SettingsOpacity,
                    0.10,
                    1.00),
            SettingsBlurOverrideEnabled =
                SettingsBlurOverrideEnabled,
            SettingsBlurRadius =
                Math.Clamp(
                    SettingsBlurRadius,
                    0,
                    100),
            RingColor =
                string.IsNullOrWhiteSpace(
                    RingColor)
                    ? "#FFFFA500"
                    : RingColor,
            RingScalePercent =
                Math.Clamp(
                    RingScalePercent,
                    50,
                    150),
            RingThicknessPercent =
                Math.Clamp(
                    RingThicknessPercent,
                    50,
                    160),
            RingFillMode =
                string.Equals(
                    RingFillMode,
                    "Free",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Free"
                    : "Used",
            CompanionScalePercent =
                Math.Clamp(
                    CompanionScalePercent,
                    50,
                    150),
            DisableDefaultHoverEffect =
                DisableDefaultHoverEffect
        };
    }
}
