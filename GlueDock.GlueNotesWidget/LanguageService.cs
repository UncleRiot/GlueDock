using System.Globalization;

namespace GlueNotes;

public sealed class LanguageService
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _languages;
    private string _languageCode;

    public LanguageService()
    {
        _languageCode =
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        _languages =
            new Dictionary<string, IReadOnlyDictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["de"] =
                    new Dictionary<string, string>
                    {
                        ["App.Title"] = "GlueNotes",
                        ["Widget.Description"] = "Notizen mit formatiertem Text, Bildern und Checklisten.",
                        ["Widget.ToolTip"] = "GlueNotes öffnen",
                        ["Settings.General"] = "Allgemein",
                        ["Settings.Title"] = "Einstellungen",
                        ["Settings.Close"] = "Schließen",
                        ["Settings.Theme"] = "Theme",
                        ["Settings.Opacity"] = "Deckkraft",
                        ["Settings.Blur"] = "Unschärfe",
                        ["Settings.ShowBorder"] = "Rahmen anzeigen",
                        ["Settings.BorderColor"] = "Rahmenfarbe",
                        ["Settings.ChooseColor"] = "Farbe wählen",
                        ["Settings.DefaultFontSize"] = "Standard-Schriftgröße",
                        ["Settings.AutoSaveDelay"] = "Autosave-Verzögerung (ms)",
                        ["Settings.MaxImageWidth"] = "Maximale Bildbreite",
                        ["Settings.Reset"] = "Zurücksetzen",
                        ["Notes.New"] = "Neue Notiz",
                        ["Notes.Untitled"] = "Neue Notiz",
                        ["Notes.Empty"] = "Noch keine Notizen",
                        ["Toolbar.Bold"] = "Fett",
                        ["Toolbar.Italic"] = "Kursiv",
                        ["Toolbar.Underline"] = "Unterstrichen",
                        ["Toolbar.TextColor"] = "Textfarbe",
                        ["Toolbar.InsertChecklist"] = "Checkliste einfügen",
                        ["Toolbar.FontSize"] = "Schriftgröße",
                        ["Editor.TitlePlaceholder"] = "Titel",
                        ["Checklist.NewItem"] = "Neuer Punkt",
                        ["Color.Default"] = "Standard",
                        ["Color.Red"] = "Rot",
                        ["Color.Orange"] = "Orange",
                        ["Color.Green"] = "Grün",
                        ["Color.Blue"] = "Blau",
                        ["Color.Purple"] = "Violett",
                        ["Status.Saved"] = "Gespeichert",
                        ["Status.Saving"] = "Speichert …"
                    },
                ["en"] =
                    new Dictionary<string, string>
                    {
                        ["App.Title"] = "GlueNotes",
                        ["Widget.Description"] = "Notes with formatted text, images and checklists.",
                        ["Widget.ToolTip"] = "Open GlueNotes",
                        ["Settings.General"] = "General",
                        ["Settings.Title"] = "Settings",
                        ["Settings.Close"] = "Close",
                        ["Settings.Theme"] = "Theme",
                        ["Settings.Opacity"] = "Opacity",
                        ["Settings.Blur"] = "Blur",
                        ["Settings.ShowBorder"] = "Show border",
                        ["Settings.BorderColor"] = "Border color",
                        ["Settings.ChooseColor"] = "Choose color",
                        ["Settings.DefaultFontSize"] = "Default font size",
                        ["Settings.AutoSaveDelay"] = "Autosave delay (ms)",
                        ["Settings.MaxImageWidth"] = "Maximum image width",
                        ["Settings.Reset"] = "Reset",
                        ["Notes.New"] = "New note",
                        ["Notes.Untitled"] = "New note",
                        ["Notes.Empty"] = "No notes yet",
                        ["Toolbar.Bold"] = "Bold",
                        ["Toolbar.Italic"] = "Italic",
                        ["Toolbar.Underline"] = "Underline",
                        ["Toolbar.TextColor"] = "Text color",
                        ["Toolbar.InsertChecklist"] = "Insert checklist",
                        ["Toolbar.FontSize"] = "Font size",
                        ["Editor.TitlePlaceholder"] = "Title",
                        ["Checklist.NewItem"] = "New item",
                        ["Color.Default"] = "Default",
                        ["Color.Red"] = "Red",
                        ["Color.Orange"] = "Orange",
                        ["Color.Green"] = "Green",
                        ["Color.Blue"] = "Blue",
                        ["Color.Purple"] = "Purple",
                        ["Status.Saved"] = "Saved",
                        ["Status.Saving"] = "Saving …"
                    }
            };
    }

    public void SetLanguageCode(
        string? languageCode)
    {
        string normalized =
            string.IsNullOrWhiteSpace(
                languageCode)
                ? "en"
                : languageCode
                    .Trim()
                    .ToLowerInvariant();

        _languageCode =
            _languages.ContainsKey(
                normalized)
                ? normalized
                : "en";
    }

    public string this[
        string key]
    {
        get
        {
            if (!_languages.TryGetValue(
                    _languageCode,
                    out IReadOnlyDictionary<string, string>? language))
            {
                language =
                    _languages["en"];
            }

            if (language.TryGetValue(
                    key,
                    out string? value))
            {
                return value;
            }

            return
                _languages["en"].TryGetValue(
                    key,
                    out value)
                    ? value
                    : key;
        }
    }
}
