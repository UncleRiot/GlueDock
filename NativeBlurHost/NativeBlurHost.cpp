#include <windows.h>
#include <d2d1effects.h>
#include <Windows.Foundation.h>
#include <Windows.Graphics.Effects.h>
#include <Windows.Graphics.Effects.Interop.h>
#include <Windows.UI.Composition.Interop.h>
#include <wrl.h>
#include <wrl/wrappers/corewrappers.h>
#include <winrt/base.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Graphics.Effects.h>
#include <winrt/Windows.UI.Composition.h>
#include <winrt/Windows.UI.Composition.Desktop.h>

#include <algorithm>
#include <memory>

using Microsoft::WRL::ComPtr;
using Microsoft::WRL::Make;
using Microsoft::WRL::RuntimeClass;
using Microsoft::WRL::RuntimeClassFlags;
using Microsoft::WRL::WinRtClassicComMix;
using Microsoft::WRL::Wrappers::HString;
using Microsoft::WRL::Wrappers::HStringReference;

namespace
{
    class GaussianBlurEffect final :
        public RuntimeClass<
            RuntimeClassFlags<WinRtClassicComMix>,
            ABI::Windows::Graphics::Effects::IGraphicsEffect,
            ABI::Windows::Graphics::Effects::IGraphicsEffectSource,
            ABI::Windows::Graphics::Effects::IGraphicsEffectD2D1Interop>
    {
        InspectableClass(
            L"GlueDock.NativeGaussianBlurEffect",
            BaseTrust);

    public:
        GaussianBlurEffect()
        {
            _name.Set(
                L"Blur");
        }

        HRESULT SetSource(
            ABI::Windows::Graphics::Effects::IGraphicsEffectSource* source)
        {
            _source =
                source;

            return
                S_OK;
        }

        void SetBlurAmount(
            float blurAmount)
        {
            _blurAmount =
                std::clamp(
                    blurAmount,
                    0.0f,
                    250.0f);
        }

        IFACEMETHODIMP get_Name(
            HSTRING* value) override
        {
            if (value == nullptr)
            {
                return
                    E_POINTER;
            }

            return
                _name.CopyTo(
                    value);
        }

        IFACEMETHODIMP put_Name(
            HSTRING value) override
        {
            return
                _name.Set(
                    value);
        }

        IFACEMETHODIMP GetEffectId(
            GUID* id) override
        {
            if (id == nullptr)
            {
                return
                    E_POINTER;
            }

            *id =
                GUID
                {
                    0x1feb6d69,
                    0x2fe6,
                    0x4ac9,
                    {
                        0x8c,
                        0x58,
                        0x1d,
                        0x7f,
                        0x93,
                        0xe7,
                        0xa6,
                        0xa5
                    }
                };

            return
                S_OK;
        }

        IFACEMETHODIMP GetNamedPropertyMapping(
            LPCWSTR name,
            UINT* index,
            ABI::Windows::Graphics::Effects::GRAPHICS_EFFECT_PROPERTY_MAPPING* mapping)
            override
        {
            if (name == nullptr ||
                index == nullptr ||
                mapping == nullptr)
            {
                return
                    E_POINTER;
            }

            if (_wcsicmp(
                    name,
                    L"BlurAmount") == 0)
            {
                *index =
                    D2D1_GAUSSIANBLUR_PROP_STANDARD_DEVIATION;

                *mapping =
                    ABI::Windows::Graphics::Effects::
                        GRAPHICS_EFFECT_PROPERTY_MAPPING_DIRECT;

                return
                    S_OK;
            }

            if (_wcsicmp(
                    name,
                    L"Optimization") == 0)
            {
                *index =
                    D2D1_GAUSSIANBLUR_PROP_OPTIMIZATION;

                *mapping =
                    ABI::Windows::Graphics::Effects::
                        GRAPHICS_EFFECT_PROPERTY_MAPPING_DIRECT;

                return
                    S_OK;
            }

            if (_wcsicmp(
                    name,
                    L"BorderMode") == 0)
            {
                *index =
                    D2D1_GAUSSIANBLUR_PROP_BORDER_MODE;

                *mapping =
                    ABI::Windows::Graphics::Effects::
                        GRAPHICS_EFFECT_PROPERTY_MAPPING_DIRECT;

                return
                    S_OK;
            }

            return
                E_INVALIDARG;
        }

        IFACEMETHODIMP GetProperty(
            UINT index,
            ABI::Windows::Foundation::IPropertyValue** value)
            override
        {
            if (value == nullptr)
            {
                return
                    E_POINTER;
            }

            *value =
                nullptr;

            ComPtr<
                ABI::Windows::Foundation::
                    IPropertyValueStatics> statics;

            HRESULT result =
                GetActivationFactory(
                    HStringReference(
                        RuntimeClass_Windows_Foundation_PropertyValue)
                        .Get(),
                    &statics);

            if (FAILED(
                    result))
            {
                return
                    result;
            }

            switch (index)
            {
                case D2D1_GAUSSIANBLUR_PROP_STANDARD_DEVIATION:
                    return
                        statics->CreateSingle(
                            _blurAmount,
                            reinterpret_cast<IInspectable**>(
                                value));

                case D2D1_GAUSSIANBLUR_PROP_OPTIMIZATION:
                    return
                        statics->CreateUInt32(
                            static_cast<UINT32>(
                                D2D1_GAUSSIANBLUR_OPTIMIZATION_BALANCED),
                            reinterpret_cast<IInspectable**>(
                                value));

                case D2D1_GAUSSIANBLUR_PROP_BORDER_MODE:
                    return
                        statics->CreateUInt32(
                            static_cast<UINT32>(
                                D2D1_BORDER_MODE_HARD),
                            reinterpret_cast<IInspectable**>(
                                value));

                default:
                    return
                        E_INVALIDARG;
            }
        }

        IFACEMETHODIMP GetPropertyCount(
            UINT* count) override
        {
            if (count == nullptr)
            {
                return
                    E_POINTER;
            }

            *count =
                3;

            return
                S_OK;
        }

        IFACEMETHODIMP GetSource(
            UINT index,
            ABI::Windows::Graphics::Effects::IGraphicsEffectSource** source)
            override
        {
            if (source == nullptr)
            {
                return
                    E_POINTER;
            }

            *source =
                nullptr;

            if (index != 0)
            {
                return
                    E_INVALIDARG;
            }

            return
                _source.CopyTo(
                    source);
        }

        IFACEMETHODIMP GetSourceCount(
            UINT* count) override
        {
            if (count == nullptr)
            {
                return
                    E_POINTER;
            }

            *count =
                1;

            return
                S_OK;
        }

    private:
        HString _name;

        ComPtr<
            ABI::Windows::Graphics::Effects::
                IGraphicsEffectSource> _source;

        float _blurAmount =
            0.0f;
    };

    struct NativeBlurHost
    {
        winrt::Windows::UI::Composition::Compositor Compositor
        {
            nullptr
        };

        winrt::Windows::UI::Composition::Desktop::DesktopWindowTarget Target
        {
            nullptr
        };

        winrt::Windows::UI::Composition::ContainerVisual Root
        {
            nullptr
        };

        winrt::Windows::UI::Composition::CompositionEffectFactory EffectFactory
        {
            nullptr
        };

        winrt::Windows::UI::Composition::CompositionEffectBrush EffectBrush
        {
            nullptr
        };

        winrt::Windows::UI::Composition::CompositionBackdropBrush BackdropBrush
        {
            nullptr
        };

        winrt::Windows::UI::Composition::SpriteVisual BlurVisual
        {
            nullptr
        };

        HRESULT Initialize(
            HWND hwnd,
            float blurAmount)
        {
            try
            {
                Compositor =
                    winrt::Windows::UI::Composition::Compositor();

                namespace abi =
                    ABI::Windows::UI::Composition::Desktop;

                auto compositorInterop =
                    Compositor.as<
                        abi::ICompositorDesktopInterop>();

                winrt::check_hresult(
                    compositorInterop
                        ->CreateDesktopWindowTarget(
                            hwnd,
                            false,
                            reinterpret_cast<
                                abi::IDesktopWindowTarget**>(
                                    winrt::put_abi(
                                        Target))));

                Root =
                    Compositor.CreateContainerVisual();

                Root.RelativeSizeAdjustment(
                    {
                        1.0f,
                        1.0f
                    });

                Target.Root(
                    Root);

                auto effect =
                    Make<GaussianBlurEffect>();

                if (!effect)
                {
                    return
                        E_OUTOFMEMORY;
                }

                effect->SetBlurAmount(
                    blurAmount);

                auto sourceParameter =
                    winrt::Windows::UI::Composition::
                        CompositionEffectSourceParameter(
                            L"Backdrop");

                auto source =
                    reinterpret_cast<
                        ABI::Windows::Graphics::Effects::
                            IGraphicsEffectSource*>(
                                winrt::get_abi(
                                    sourceParameter));

                winrt::check_hresult(
                    effect->SetSource(
                        source));

                ComPtr<
                    ABI::Windows::Graphics::Effects::
                        IGraphicsEffect> effectAbi;

                winrt::check_hresult(
                    effect.As(
                        &effectAbi));

                winrt::Windows::Graphics::Effects::
                    IGraphicsEffect effectGraph
                    {
                        nullptr
                    };

                winrt::copy_from_abi(
                    effectGraph,
                    effectAbi.Get());

                EffectFactory =
                    Compositor.CreateEffectFactory(
                        effectGraph,
                        {
                            L"Blur.BlurAmount"
                        });

                EffectBrush =
                    EffectFactory.CreateBrush();

                BackdropBrush =
                    Compositor.CreateBackdropBrush();

                EffectBrush.SetSourceParameter(
                    L"Backdrop",
                    BackdropBrush);

                BlurVisual =
                    Compositor.CreateSpriteVisual();

                BlurVisual.RelativeSizeAdjustment(
                    {
                        1.0f,
                        1.0f
                    });

                BlurVisual.Brush(
                    EffectBrush);

                Root.Children().InsertAtTop(
                    BlurVisual);

                return
                    SetBlurAmount(
                        blurAmount);
            }
            catch (winrt::hresult_error const& error)
            {
                return
                    error.code();
            }
            catch (...)
            {
                return
                    E_FAIL;
            }
        }

        HRESULT SetBlurAmount(
            float blurAmount)
        {
            try
            {
                if (!EffectBrush)
                {
                    return
                        E_POINTER;
                }

                EffectBrush.Properties().InsertScalar(
                    L"Blur.BlurAmount",
                    std::clamp(
                        blurAmount,
                        0.0f,
                        250.0f));

                return
                    S_OK;
            }
            catch (winrt::hresult_error const& error)
            {
                return
                    error.code();
            }
            catch (...)
            {
                return
                    E_FAIL;
            }
        }
    };
}

extern "C"
{
    __declspec(dllexport)
    void* __stdcall GlueDockBlur_Create(
        HWND hwnd,
        float blurAmount,
        HRESULT* result)
    {
        if (result == nullptr)
        {
            return
                nullptr;
        }

        *result =
            E_FAIL;

        if (hwnd == nullptr)
        {
            *result =
                E_INVALIDARG;

            return
                nullptr;
        }

        try
        {
            auto host =
                std::make_unique<
                    NativeBlurHost>();

            *result =
                host->Initialize(
                    hwnd,
                    blurAmount);

            if (FAILED(
                    *result))
            {
                return
                    nullptr;
            }

            return
                host.release();
        }
        catch (...)
        {
            *result =
                E_FAIL;

            return
                nullptr;
        }
    }

    __declspec(dllexport)
    HRESULT __stdcall GlueDockBlur_SetAmount(
        void* host,
        float blurAmount)
    {
        if (host == nullptr)
        {
            return
                E_INVALIDARG;
        }

        return
            static_cast<
                NativeBlurHost*>(
                    host)
                ->SetBlurAmount(
                    blurAmount);
    }

    __declspec(dllexport)
    void __stdcall GlueDockBlur_Destroy(
        void* host)
    {
        delete
            static_cast<
                NativeBlurHost*>(
                    host);
    }
}
