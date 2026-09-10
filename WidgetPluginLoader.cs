using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;

namespace GlueDock;

public static class WidgetPluginLoader
{
    private static readonly Dictionary<string, Type> WidgetTypes =
        new(
            StringComparer.Ordinal);

    private static readonly object SyncRoot =
        new();

    public static bool IsRuntimeWidgetPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            return false;
        }

        try
        {
            string widgetRoot =
                Path.GetFullPath(
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "GlueDock_Widgets"))
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            string fullPath =
                Path.GetFullPath(
                    path);

            return fullPath.StartsWith(
                widgetRoot,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static string ResolveRuntimeWidgetPath(
        string path)
    {
        if (string.IsNullOrWhiteSpace(
                path))
        {
            return path;
        }

        try
        {
            string fullPath =
                Path.GetFullPath(
                    path);

            if (IsRuntimeWidgetPath(
                    fullPath))
            {
                return fullPath;
            }

            if (!string.Equals(
                    Path.GetExtension(
                        fullPath),
                    ".dll",
                    StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            string fileName =
                Path.GetFileName(
                    fullPath);

            string widgetName =
                Path.GetFileNameWithoutExtension(
                    fullPath);

            string runtimePath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "GlueDock_Widgets",
                    widgetName,
                    fileName);

            if (!File.Exists(
                    runtimePath))
            {
                return path;
            }

            DebugLog.Write(
                "Widget",
                $"Runtime path resolved; Source={path}; Runtime={runtimePath}");

            return runtimePath;
        }
        catch
        {
            return path;
        }
    }

    public static string ResolveExistingInstanceId(
        string assemblyPath,
        string preferredInstanceId)
    {
        assemblyPath =
            ResolveRuntimeWidgetPath(
                assemblyPath);

        if (string.IsNullOrWhiteSpace(
                assemblyPath) ||
            string.IsNullOrWhiteSpace(
                preferredInstanceId) ||
            !File.Exists(
                assemblyPath) ||
            !IsRuntimeWidgetPath(
                assemblyPath) ||
            !string.Equals(
                Path.GetExtension(
                    assemblyPath),
                ".dll",
                StringComparison.OrdinalIgnoreCase))
        {
            return preferredInstanceId;
        }

        try
        {
            AssemblyName assemblyName =
                AssemblyName.GetAssemblyName(
                    assemblyPath);

            string widgetName =
                assemblyName.Name ??
                Path.GetFileNameWithoutExtension(
                    assemblyPath);

            string instancesDirectory =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "GlueDock_Widgets",
                    widgetName,
                    "Instances");

            string preferredDirectory =
                Path.Combine(
                    instancesDirectory,
                    preferredInstanceId);

            if (HasInstanceData(
                    preferredDirectory))
            {
                return preferredInstanceId;
            }

            if (!Directory.Exists(
                    instancesDirectory))
            {
                return preferredInstanceId;
            }

            List<string> reusableInstanceIds =
                Directory.GetDirectories(
                        instancesDirectory,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .Where(
                        HasInstanceData)
                    .Select(
                        Path.GetFileName)
                    .Where(
                        instanceId =>
                            !string.IsNullOrWhiteSpace(
                                instanceId))
                    .Cast<string>()
                    .ToList();

            if (reusableInstanceIds.Count != 1)
            {
                return preferredInstanceId;
            }

            string reusableInstanceId =
                reusableInstanceIds[0];

            DebugLog.Write(
                "Widget",
                $"Existing instance reused; Path={assemblyPath}; RequestedId={preferredInstanceId}; ReusedId={reusableInstanceId}");

            return reusableInstanceId;
        }
        catch (Exception ex)
        {
            DebugLog.Write(
                "Widget",
                $"Existing instance lookup failed; Path={assemblyPath}; RequestedId={preferredInstanceId}; Type={ex.GetType().Name}; Message={ex.Message}");

            return preferredInstanceId;
        }
    }

    private static bool HasInstanceData(
        string instanceDirectory)
    {
        if (!Directory.Exists(
                instanceDirectory))
        {
            return false;
        }

        string settingsPath =
            Path.Combine(
                instanceDirectory,
                "settings.json");

        string statePath =
            Path.Combine(
                instanceDirectory,
                "state.json");

        return File.Exists(
                   settingsPath) ||
               File.Exists(
                   statePath);
    }

    public static IReadOnlyList<WidgetPluginInfo> GetAvailableWidgets()
    {
        List<WidgetPluginInfo> widgets =
            [];

        string widgetRoot =
            Path.Combine(
                AppContext.BaseDirectory,
                "GlueDock_Widgets");

        try
        {
            Directory.CreateDirectory(
                widgetRoot);

            foreach (string widgetDirectory in Directory.GetDirectories(
                         widgetRoot,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                foreach (string assemblyPath in Directory.GetFiles(
                             widgetDirectory,
                             "*.dll",
                             SearchOption.TopDirectoryOnly))
                {
                    if (TryGetMetadata(
                            assemblyPath,
                            out WidgetPluginInfo? widgetInfo) &&
                        widgetInfo is not null)
                    {
                        widgets.Add(
                            widgetInfo);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write(
                "Widget",
                $"Discovery failed; Root={widgetRoot}; Type={ex.GetType().Name}; Message={ex.Message}");
        }

        return widgets
            .OrderBy(
                widget =>
                    widget.DisplayName,
                StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(
                widget =>
                    widget.DllName,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool TryGetMetadata(
        string assemblyPath,
        out WidgetPluginInfo? widgetInfo)
    {
        widgetInfo = null;

        assemblyPath =
            ResolveRuntimeWidgetPath(
                assemblyPath);

        if (string.IsNullOrWhiteSpace(
                assemblyPath) ||
            !File.Exists(
                assemblyPath) ||
            !IsRuntimeWidgetPath(
                assemblyPath) ||
            !string.Equals(
                Path.GetExtension(
                    assemblyPath),
                ".dll",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        IGlueDockWidget? widget =
            null;

        try
        {
            if (!TryGetWidgetType(
                    assemblyPath,
                    out _,
                    out Type? widgetType) ||
                widgetType is null)
            {
                return false;
            }

            widget =
                (IGlueDockWidget?)Activator.CreateInstance(
                    widgetType);

            if (widget is null)
            {
                return false;
            }

            widgetInfo =
                new WidgetPluginInfo(
                    widget.DisplayName,
                    widget.DisplayNameKey,
                    widget.Description,
                    widget.DescriptionKey,
                    Path.GetFileName(
                        assemblyPath),
                    assemblyPath);

            DebugLog.Write(
                "Widget",
                $"Discovered; Path={assemblyPath}; Name={widgetInfo.DisplayName}; Dll={widgetInfo.DllName}");

            return true;
        }
        catch (Exception ex)
        {
            DebugLog.Write(
                "Widget",
                $"Discovery rejected; Path={assemblyPath}; Type={ex.GetType().Name}; Message={ex.Message}");

            widgetInfo = null;
            return false;
        }
        finally
        {
            if (widget is not null)
            {
                try
                {
                    widget.Dispose();
                }
                catch (Exception ex)
                {
                    DebugLog.Write(
                        "Widget",
                        $"Discovery dispose failed; Path={assemblyPath}; Type={ex.GetType().Name}; Message={ex.Message}");
                }
            }
        }
    }

    private static bool TryGetWidgetType(
        string assemblyPath,
        out AssemblyName? assemblyName,
        out Type? widgetType)
    {
        assemblyName = null;
        widgetType = null;

        try
        {
            assemblyName =
                AssemblyName.GetAssemblyName(
                    assemblyPath);

            string cacheKey =
                assemblyName.FullName ??
                assemblyName.Name ??
                assemblyPath;

            lock (SyncRoot)
            {
                if (WidgetTypes.TryGetValue(
                        cacheKey,
                        out widgetType))
                {
                    return true;
                }

                using FileStream stream =
                    File.Open(
                        assemblyPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);

                Assembly assembly =
                    AssemblyLoadContext.Default.LoadFromStream(
                        stream);

                widgetType =
                    assembly
                        .GetTypes()
                        .FirstOrDefault(
                            type =>
                                type.IsClass &&
                                !type.IsAbstract &&
                                type.IsPublic &&
                                typeof(IGlueDockWidget).IsAssignableFrom(
                                    type) &&
                                type.GetConstructor(
                                    Type.EmptyTypes) is not null);

                if (widgetType is null)
                {
                    return false;
                }

                WidgetTypes[cacheKey] =
                    widgetType;

                return true;
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write(
                "Widget",
                $"Type detection failed; Path={assemblyPath}; Type={ex.GetType().Name}; Message={ex.Message}");

            assemblyName = null;
            widgetType = null;
            return false;
        }
    }

    public static bool TryCreate(
        string assemblyPath,
        string instanceId,
        Action<IGlueDockWidget, string>? openSettingsSection,
        out IGlueDockWidget? widget,
        out FrameworkElement? view)
    {
        widget = null;
        view = null;

        assemblyPath =
            ResolveRuntimeWidgetPath(
                assemblyPath);

        if (string.IsNullOrWhiteSpace(assemblyPath) ||
            !File.Exists(assemblyPath) ||
            !IsRuntimeWidgetPath(
                assemblyPath) ||
            !string.Equals(
                Path.GetExtension(assemblyPath),
                ".dll",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            if (!TryGetWidgetType(
                    assemblyPath,
                    out AssemblyName? assemblyName,
                    out Type? widgetType) ||
                assemblyName is null ||
                widgetType is null)
            {
                return false;
            }

            widget =
                (IGlueDockWidget?)Activator.CreateInstance(
                    widgetType);

            if (widget is null)
            {
                return false;
            }

            IGlueDockWidget initializedWidget =
                widget;

            string widgetDirectory =
                Path.GetDirectoryName(
                    Path.GetFullPath(
                        assemblyPath)) ??
                AppContext.BaseDirectory;

            string widgetName =
                assemblyName.Name ??
                widgetType.Name;

            string settingsDirectory =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "GlueDock_Widgets",
                    widgetName,
                    "Instances",
                    instanceId);

            Directory.CreateDirectory(
                settingsDirectory);

            widget.Initialize(
                new GlueDockWidgetContext
                {
                    InstanceId = instanceId,
                    WidgetDirectory = widgetDirectory,
                    SettingsDirectory = settingsDirectory,
                    Log =
                        (category, message) =>
                            DebugLog.Write(
                                category,
                                message),
                    Localize =
                        key =>
                            App.Language[key],
                    SubscribeLanguageChanged =
                        handler =>
                            App.Language.LanguageChanged +=
                                handler,
                    UnsubscribeLanguageChanged =
                        handler =>
                            App.Language.LanguageChanged -=
                                handler,
                    GetLanguageCode =
                        () =>
                            App.Language.CurrentLanguageCode,
                    OpenSettingsSection =
                        sectionId =>
                            openSettingsSection?.Invoke(
                                initializedWidget,
                                sectionId)
                });

            view =
                widget.CreateView();

            if (view is null)
            {
                widget.Dispose();
                widget = null;
                return false;
            }

            DebugLog.Write(
                "Widget",
                $"Loaded; Path={assemblyPath}; Type={widgetType.FullName}; Name={widget.DisplayName}");

            return true;
        }
        catch (Exception ex)
        {
            DebugLog.Write(
                "Widget",
                $"Load failed; Path={assemblyPath}; Type={ex.GetType().Name}; Message={ex.Message}");

            widget?.Dispose();
            widget = null;
            view = null;

            return false;
        }
    }
}

public sealed record WidgetPluginInfo(
    string DisplayName,
    string DisplayNameKey,
    string Description,
    string DescriptionKey,
    string DllName,
    string AssemblyPath);

