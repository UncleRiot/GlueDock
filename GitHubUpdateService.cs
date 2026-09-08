using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GlueDock;

public enum GitHubUpdateErrorKind
{
    None,
    Timeout,
    Network,
    Http,
    InvalidJson,
    InvalidResponse,
    InvalidVersion,
    Unexpected
}

public sealed class GitHubUpdateResult
{
    public bool CanConnectToGitHub { get; set; }

    public bool UpdateAvailable { get; set; }

    public string LatestVersion { get; set; } = string.Empty;

    public string DownloadUrl { get; set; } = string.Empty;

    public GitHubUpdateErrorKind ErrorKind { get; set; }
}

public static class GitHubUpdateService
{
    public const string RepositoryUrl =
        "https://github.com/UncleRiot/GlueDock";

    public const string ReleasesUrl =
        "https://github.com/UncleRiot/GlueDock/releases";

    private const string ReleasesApiUrl =
        "https://api.github.com/repos/UncleRiot/GlueDock/releases?per_page=100";

    private static readonly TimeSpan RequestTimeout =
        TimeSpan.FromSeconds(10);

    private static readonly Regex SemanticVersionRegex =
        new(
            @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    public static async Task<GitHubUpdateResult> CheckForUpdateAsync()
    {
        string currentVersionText =
            GetApplicationVersionText();

        try
        {
            using HttpClient httpClient =
                new()
                {
                    Timeout = RequestTimeout
                };

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "GlueDock/" +
                currentVersionText);

            using HttpResponseMessage response =
                await httpClient.GetAsync(
                    ReleasesApiUrl);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content.ReadAsStringAsync();

            using JsonDocument document =
                JsonDocument.Parse(json);

            if (document.RootElement.ValueKind !=
                JsonValueKind.Array)
            {
                return CreateFailure(
                    GitHubUpdateErrorKind.InvalidResponse,
                    true);
            }

            if (!TryParseSemanticVersion(
                    NormalizeVersionText(
                        currentVersionText),
                    out SemanticVersion currentVersion))
            {
                return CreateFailure(
                    GitHubUpdateErrorKind.InvalidVersion,
                    true);
            }

            bool releaseFound = false;
            SemanticVersion latestVersion = default;
            string latestVersionText = string.Empty;
            string downloadUrl = ReleasesUrl;

            foreach (JsonElement release
                     in document.RootElement.EnumerateArray())
            {
                if (release.ValueKind !=
                    JsonValueKind.Object)
                {
                    continue;
                }

                if (release.TryGetProperty(
                        "draft",
                        out JsonElement draft) &&
                    draft.ValueKind ==
                    JsonValueKind.True)
                {
                    continue;
                }

                if (!release.TryGetProperty(
                        "tag_name",
                        out JsonElement tagName) ||
                    tagName.ValueKind !=
                    JsonValueKind.String)
                {
                    continue;
                }

                string candidateText =
                    NormalizeVersionText(
                        tagName.GetString());

                if (!TryParseSemanticVersion(
                        candidateText,
                        out SemanticVersion candidate))
                {
                    continue;
                }

                if (releaseFound &&
                    candidate.CompareTo(
                        latestVersion) <= 0)
                {
                    continue;
                }

                releaseFound = true;
                latestVersion = candidate;
                latestVersionText = candidateText;

                if (release.TryGetProperty(
                        "html_url",
                        out JsonElement htmlUrl) &&
                    htmlUrl.ValueKind ==
                    JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(
                        htmlUrl.GetString()))
                {
                    downloadUrl =
                        htmlUrl.GetString()!;
                }
                else
                {
                    downloadUrl =
                        ReleasesUrl;
                }
            }

            if (!releaseFound)
            {
                return CreateFailure(
                    GitHubUpdateErrorKind.InvalidVersion,
                    true);
            }

            return new GitHubUpdateResult
            {
                CanConnectToGitHub = true,
                UpdateAvailable =
                    latestVersion.CompareTo(
                        currentVersion) > 0,
                LatestVersion =
                    latestVersionText,
                DownloadUrl =
                    downloadUrl,
                ErrorKind =
                    GitHubUpdateErrorKind.None
            };
        }
        catch (TaskCanceledException)
        {
            return CreateFailure(
                GitHubUpdateErrorKind.Timeout,
                false);
        }
        catch (HttpRequestException exception)
        {
            return CreateFailure(
                exception.StatusCode.HasValue
                    ? GitHubUpdateErrorKind.Http
                    : GitHubUpdateErrorKind.Network,
                false);
        }
        catch (JsonException)
        {
            return CreateFailure(
                GitHubUpdateErrorKind.InvalidJson,
                true);
        }
        catch
        {
            return CreateFailure(
                GitHubUpdateErrorKind.Unexpected,
                false);
        }
    }

    public static string GetApplicationVersionText()
    {
        Assembly assembly =
            typeof(GitHubUpdateService).Assembly;

        AssemblyInformationalVersionAttribute? attribute =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (!string.IsNullOrWhiteSpace(
                attribute?.InformationalVersion))
        {
            return attribute.InformationalVersion
                .Split('+')[0];
        }

        Version? version =
            assembly.GetName().Version;

        if (version is null)
        {
            return "Unknown";
        }

        return
            $"{version.Major}.{version.Minor}.{version.Build}";
    }

    public static bool IsApplicationVersionNewerThan(
        string? previousVersionText)
    {
        if (!TryParseSemanticVersion(
                NormalizeVersionText(
                    GetApplicationVersionText()),
                out SemanticVersion currentVersion))
        {
            return false;
        }

        if (!TryParseSemanticVersion(
                NormalizeVersionText(
                    previousVersionText),
                out SemanticVersion previousVersion))
        {
            return true;
        }

        return
            currentVersion.CompareTo(
                previousVersion) > 0;
    }

    private static GitHubUpdateResult CreateFailure(
        GitHubUpdateErrorKind errorKind,
        bool canConnectToGitHub)
    {
        DebugLog.Write(
            "Update",
            $"Check failed; Error={errorKind}; CanConnect={canConnectToGitHub}");

        return new GitHubUpdateResult
        {
            CanConnectToGitHub =
                canConnectToGitHub,
            UpdateAvailable =
                false,
            ErrorKind =
                errorKind
        };
    }

    private static string NormalizeVersionText(
        string? versionText)
    {
        if (string.IsNullOrWhiteSpace(
                versionText))
        {
            return string.Empty;
        }

        return versionText
            .Trim()
            .TrimStart(
                'v',
                'V');
    }

    private static bool TryParseSemanticVersion(
        string versionText,
        out SemanticVersion semanticVersion)
    {
        semanticVersion = default;

        Match match =
            SemanticVersionRegex.Match(
                versionText);

        if (!match.Success ||
            !int.TryParse(
                match.Groups["major"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int major) ||
            !int.TryParse(
                match.Groups["minor"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int minor) ||
            !int.TryParse(
                match.Groups["patch"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int patch))
        {
            return false;
        }

        semanticVersion =
            new SemanticVersion(
                major,
                minor,
                patch,
                match.Groups["prerelease"].Success
                    ? match.Groups["prerelease"].Value
                    : string.Empty);

        return true;
    }

    private readonly struct SemanticVersion :
        IComparable<SemanticVersion>
    {
        private readonly int _major;
        private readonly int _minor;
        private readonly int _patch;
        private readonly string _prerelease;

        public SemanticVersion(
            int major,
            int minor,
            int patch,
            string prerelease)
        {
            _major = major;
            _minor = minor;
            _patch = patch;
            _prerelease =
                prerelease ?? string.Empty;
        }

        public int CompareTo(
            SemanticVersion other)
        {
            int result =
                _major.CompareTo(
                    other._major);

            if (result != 0)
            {
                return result;
            }

            result =
                _minor.CompareTo(
                    other._minor);

            if (result != 0)
            {
                return result;
            }

            result =
                _patch.CompareTo(
                    other._patch);

            if (result != 0)
            {
                return result;
            }

            bool isPrerelease =
                !string.IsNullOrEmpty(
                    _prerelease);

            bool otherIsPrerelease =
                !string.IsNullOrEmpty(
                    other._prerelease);

            if (!isPrerelease &&
                !otherIsPrerelease)
            {
                return 0;
            }

            if (!isPrerelease)
            {
                return 1;
            }

            if (!otherIsPrerelease)
            {
                return -1;
            }

            string[] identifiers =
                _prerelease.Split('.');

            string[] otherIdentifiers =
                other._prerelease.Split('.');

            int count =
                Math.Min(
                    identifiers.Length,
                    otherIdentifiers.Length);

            for (int index = 0;
                 index < count;
                 index++)
            {
                result =
                    ComparePrereleaseIdentifier(
                        identifiers[index],
                        otherIdentifiers[index]);

                if (result != 0)
                {
                    return result;
                }
            }

            return identifiers.Length.CompareTo(
                otherIdentifiers.Length);
        }

        private static int ComparePrereleaseIdentifier(
            string identifier,
            string otherIdentifier)
        {
            bool isNumeric =
                long.TryParse(
                    identifier,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long numericValue);

            bool otherIsNumeric =
                long.TryParse(
                    otherIdentifier,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long otherNumericValue);

            if (isNumeric &&
                otherIsNumeric)
            {
                return numericValue.CompareTo(
                    otherNumericValue);
            }

            if (isNumeric)
            {
                return -1;
            }

            if (otherIsNumeric)
            {
                return 1;
            }

            return string.CompareOrdinal(
                identifier,
                otherIdentifier);
        }
    }
}
