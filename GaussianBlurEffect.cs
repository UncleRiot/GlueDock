using Windows.Graphics.Effects;
using Windows.Graphics.Effects.Interop;

namespace GlueDock;

internal enum EffectOptimization : uint
{
    Speed = 0,
    Balanced = 1,
    Quality = 2
}

internal enum EffectBorderMode : uint
{
    Soft = 0,
    Hard = 1
}

internal sealed class GaussianBlurEffect :
    IGraphicsEffect,
    IGraphicsEffectSource,
    IGraphicsEffectD2D1Interop
{
    private static readonly Guid GaussianBlurEffectId =
        new(
            "1FEB6D69-2FE6-4AC9-8C58-1D7F93E7A6A5");

    public string Name { get; set; } =
        "Blur";

    public IGraphicsEffectSource? Source { get; set; }

    public float BlurAmount { get; set; }

    public EffectOptimization Optimization { get; set; } =
        EffectOptimization.Balanced;

    public EffectBorderMode BorderMode { get; set; } =
        EffectBorderMode.Hard;

    public Guid EffectId =>
        GaussianBlurEffectId;

    public uint PropertyCount =>
        3;

    public uint SourceCount =>
        1;

    public uint GetNamedPropertyMapping(
        string name,
        out GRAPHICS_EFFECT_PROPERTY_MAPPING mapping)
    {
        switch (name)
        {
            case nameof(BlurAmount):
                mapping =
                    GRAPHICS_EFFECT_PROPERTY_MAPPING
                        .GRAPHICS_EFFECT_PROPERTY_MAPPING_DIRECT;

                return
                    0;

            case nameof(Optimization):
                mapping =
                    GRAPHICS_EFFECT_PROPERTY_MAPPING
                        .GRAPHICS_EFFECT_PROPERTY_MAPPING_DIRECT;

                return
                    1;

            case nameof(BorderMode):
                mapping =
                    GRAPHICS_EFFECT_PROPERTY_MAPPING
                        .GRAPHICS_EFFECT_PROPERTY_MAPPING_DIRECT;

                return
                    2;

            default:
                mapping =
                    GRAPHICS_EFFECT_PROPERTY_MAPPING
                        .GRAPHICS_EFFECT_PROPERTY_MAPPING_UNKNOWN;

                return
                    uint.MaxValue;
        }
    }

    public object? GetProperty(
        uint index)
    {
        return index switch
        {
            0 =>
                BlurAmount,

            1 =>
                (uint)Optimization,

            2 =>
                (uint)BorderMode,

            _ =>
                null
        };
    }

    public IGraphicsEffectSource? GetSource(
        uint index)
    {
        return
            index == 0
                ? Source
                : null;
    }
}
