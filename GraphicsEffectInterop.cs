using System.Runtime.InteropServices;
using WinRT;
using Windows.Graphics.Effects;

namespace Windows.Graphics.Effects.Interop
{
    public enum GRAPHICS_EFFECT_PROPERTY_MAPPING
    {
        GRAPHICS_EFFECT_PROPERTY_MAPPING_UNKNOWN = 0,
        GRAPHICS_EFFECT_PROPERTY_MAPPING_DIRECT = 1,
        GRAPHICS_EFFECT_PROPERTY_MAPPING_VECTORX = 2,
        GRAPHICS_EFFECT_PROPERTY_MAPPING_VECTORY = 3,
        GRAPHICS_EFFECT_PROPERTY_MAPPING_VECTORZ = 4,
        GRAPHICS_EFFECT_PROPERTY_MAPPING_VECTORW = 5,
        GRAPHICS_EFFECT_PROPERTY_MAPPING_RECT_TO_VECTOR4 = 6,
        GRAPHICS_EFFECT_PROPERTY_MAPPING_RADIANS_TO_DEGREES = 7,
        GRAPHICS_EFFECT_PROPERTY_MAPPING_COLORMATRIX_ALPHA_MODE = 8,
        GRAPHICS_EFFECT_PROPERTY_MAPPING_COLOR_TO_VECTOR3 = 9,
        GRAPHICS_EFFECT_PROPERTY_MAPPING_COLOR_TO_VECTOR4 = 10
    }

    [WindowsRuntimeType]
    [Guid("2FC57384-A068-44D7-A331-30982FCF7177")]
    public interface IGraphicsEffectD2D1Interop
    {
        Guid EffectId { get; }

        uint GetNamedPropertyMapping(
            string name,
            out GRAPHICS_EFFECT_PROPERTY_MAPPING mapping);

        object? GetProperty(
            uint index);

        uint PropertyCount { get; }

        IGraphicsEffectSource? GetSource(
            uint index);

        uint SourceCount { get; }
    }
}

namespace ABI.Windows.Graphics.Effects.Interop
{
    using global::System;
    using global::System.Runtime.InteropServices;
    using global::WinRT;
    using global::Windows.Graphics.Effects;

    [Guid("2FC57384-A068-44D7-A331-30982FCF7177")]
    internal sealed class IGraphicsEffectD2D1Interop
    {
        [UnmanagedFunctionPointer(
            CallingConvention.StdCall)]
        internal delegate int GetEffectIdDelegate(
            nint thisPtr,
            out Guid effectId);

        [UnmanagedFunctionPointer(
            CallingConvention.StdCall,
            CharSet = CharSet.Unicode)]
        internal delegate int GetNamedPropertyMappingDelegate(
            nint thisPtr,
            [MarshalAs(UnmanagedType.LPWStr)]
            string name,
            out uint index,
            out global::Windows.Graphics.Effects.Interop
                .GRAPHICS_EFFECT_PROPERTY_MAPPING mapping);

        [UnmanagedFunctionPointer(
            CallingConvention.StdCall)]
        internal delegate int GetPropertyDelegate(
            nint thisPtr,
            uint index,
            out nint value);

        [UnmanagedFunctionPointer(
            CallingConvention.StdCall)]
        internal delegate int GetPropertyCountDelegate(
            nint thisPtr,
            out uint count);

        [UnmanagedFunctionPointer(
            CallingConvention.StdCall)]
        internal delegate int GetSourceDelegate(
            nint thisPtr,
            uint index,
            out nint source);

        [UnmanagedFunctionPointer(
            CallingConvention.StdCall)]
        internal delegate int GetSourceCountDelegate(
            nint thisPtr,
            out uint count);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Vftbl
        {
            internal global::WinRT.Interop.IUnknownVftbl IUnknownVftbl;
            internal GetEffectIdDelegate GetEffectId;
            internal GetNamedPropertyMappingDelegate GetNamedPropertyMapping;
            internal GetPropertyDelegate GetProperty;
            internal GetPropertyCountDelegate GetPropertyCount;
            internal GetSourceDelegate GetSource;
            internal GetSourceCountDelegate GetSourceCount;

            internal static readonly Vftbl AbiToProjectionVftable;
            internal static readonly nint AbiToProjectionVftablePtr;

            static Vftbl()
            {
                AbiToProjectionVftable =
                    new Vftbl
                    {
                        IUnknownVftbl =
                            global::WinRT.Interop.IUnknownVftbl
                                .AbiToProjectionVftbl,

                        GetEffectId =
                            DoGetEffectId,

                        GetNamedPropertyMapping =
                            DoGetNamedPropertyMapping,

                        GetProperty =
                            DoGetProperty,

                        GetPropertyCount =
                            DoGetPropertyCount,

                        GetSource =
                            DoGetSource,

                        GetSourceCount =
                            DoGetSourceCount
                    };

                AbiToProjectionVftablePtr =
                    Marshal.AllocHGlobal(
                        Marshal.SizeOf<Vftbl>());

                Marshal.StructureToPtr(
                    AbiToProjectionVftable,
                    AbiToProjectionVftablePtr,
                    false);
            }

            private static int DoGetEffectId(
                nint thisPtr,
                out Guid effectId)
            {
                effectId =
                    default;

                try
                {
                    effectId =
                        ComWrappersSupport
                            .FindObject<
                                global::Windows.Graphics.Effects.Interop
                                    .IGraphicsEffectD2D1Interop>(
                                thisPtr)
                            .EffectId;

                    return
                        0;
                }
                catch (Exception ex)
                {
                    return
                        Marshal.GetHRForException(
                            ex);
                }
            }

            private static int DoGetNamedPropertyMapping(
                nint thisPtr,
                string name,
                out uint index,
                out global::Windows.Graphics.Effects.Interop
                    .GRAPHICS_EFFECT_PROPERTY_MAPPING mapping)
            {
                index =
                    uint.MaxValue;

                mapping =
                    global::Windows.Graphics.Effects.Interop
                        .GRAPHICS_EFFECT_PROPERTY_MAPPING
                        .GRAPHICS_EFFECT_PROPERTY_MAPPING_UNKNOWN;

                try
                {
                    global::Windows.Graphics.Effects.Interop
                        .IGraphicsEffectD2D1Interop instance =
                        ComWrappersSupport
                            .FindObject<
                                global::Windows.Graphics.Effects.Interop
                                    .IGraphicsEffectD2D1Interop>(
                                thisPtr);

                    index =
                        instance.GetNamedPropertyMapping(
                            name,
                            out mapping);

                    return
                        0;
                }
                catch (Exception ex)
                {
                    return
                        Marshal.GetHRForException(
                            ex);
                }
            }

            private static int DoGetProperty(
                nint thisPtr,
                uint index,
                out nint value)
            {
                value =
                    0;

                try
                {
                    object? property =
                        ComWrappersSupport
                            .FindObject<
                                global::Windows.Graphics.Effects.Interop
                                    .IGraphicsEffectD2D1Interop>(
                                thisPtr)
                            .GetProperty(
                                index);

                    if (property is null)
                    {
                        return
                            unchecked(
                                (int)0x80070057);
                    }

                    object propertyValue =
                        property switch
                        {
                            float floatValue =>
                                global::Windows.Foundation.PropertyValue
                                    .CreateSingle(
                                        floatValue),

                            uint uintValue =>
                                global::Windows.Foundation.PropertyValue
                                    .CreateUInt32(
                                        uintValue),

                            _ =>
                                property
                        };

                    value =
                        Marshal.GetIUnknownForObject(
                            propertyValue);

                    return
                        0;
                }
                catch (Exception ex)
                {
                    value =
                        0;

                    return
                        Marshal.GetHRForException(
                            ex);
                }
            }

            private static int DoGetPropertyCount(
                nint thisPtr,
                out uint count)
            {
                count =
                    0;

                try
                {
                    count =
                        ComWrappersSupport
                            .FindObject<
                                global::Windows.Graphics.Effects.Interop
                                    .IGraphicsEffectD2D1Interop>(
                                thisPtr)
                            .PropertyCount;

                    return
                        0;
                }
                catch (Exception ex)
                {
                    return
                        Marshal.GetHRForException(
                            ex);
                }
            }

            private static int DoGetSource(
                nint thisPtr,
                uint index,
                out nint source)
            {
                source =
                    0;

                try
                {
                    IGraphicsEffectSource? effectSource =
                        ComWrappersSupport
                            .FindObject<
                                global::Windows.Graphics.Effects.Interop
                                    .IGraphicsEffectD2D1Interop>(
                                thisPtr)
                            .GetSource(
                                index);

                    if (effectSource is null)
                    {
                        return
                            unchecked(
                                (int)0x80070057);
                    }

                    source =
                        Marshal.GetIUnknownForObject(
                            effectSource);

                    return
                        0;
                }
                catch (Exception ex)
                {
                    source =
                        0;

                    return
                        Marshal.GetHRForException(
                            ex);
                }
            }

            private static int DoGetSourceCount(
                nint thisPtr,
                out uint count)
            {
                count =
                    0;

                try
                {
                    count =
                        ComWrappersSupport
                            .FindObject<
                                global::Windows.Graphics.Effects.Interop
                                    .IGraphicsEffectD2D1Interop>(
                                thisPtr)
                            .SourceCount;

                    return
                        0;
                }
                catch (Exception ex)
                {
                    return
                        Marshal.GetHRForException(
                            ex);
                }
            }
        }

    }
}
