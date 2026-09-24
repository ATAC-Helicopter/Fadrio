using System.Globalization;
using System.Text;
using Fadrio.Core;

namespace Fadrio.Cli;

public static class DiagnosticTextRenderer
{
    public static string Render(RuntimeApplication application, bool redacted)
    {
        ArgumentNullException.ThrowIfNull(application);

        var output = new StringBuilder();
        ApplicationIdentity identity = application.Identity;
        Append(output, "Application", identity.DisplayName);
        Append(output, "Canonical ID", identity.Id.Value);
        Append(output, "Confidence", identity.Confidence.ToString());
        Append(output, "Desktop file", identity.DesktopFileId);
        Append(output, "Executable", Redact(identity.ExecutablePath, redacted));
        Append(output, "Executable name", identity.ExecutableName);
        Append(output, "Flatpak ID", identity.FlatpakId);
        Append(output, "Snap ID", identity.SnapId);
        Append(output, "Steam app ID", identity.SteamAppId);
        Append(output, "Wine executable", Redact(identity.WineExecutable, redacted));
        Append(output, "Icon", identity.Icon?.Value);

        output.AppendLine();
        output.Append("Identity evidence (")
            .Append(identity.Evidence.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine("): ");
        if (identity.Evidence.Count == 0)
        {
            output.AppendLine("  (none)");
        }
        else
        {
            foreach (IdentityEvidence evidence in identity.Evidence
                .OrderBy(item => item.Kind)
                .ThenBy(item => item.Value, StringComparer.Ordinal))
            {
                bool sensitive = evidence.Kind is IdentityEvidenceKind.ProcessExecutable
                    or IdentityEvidenceKind.ProcessEnvironment;
                string? value = sensitive ? Redact(evidence.Value, redacted) : evidence.Value;
                output.Append("  - ")
                    .Append(evidence.Kind)
                    .Append(": ")
                    .Append(SingleLine(value))
                    .Append(" — ")
                    .AppendLine(SingleLine(evidence.Description));
            }
        }

        output.AppendLine();
        output.Append("Sessions (")
            .Append(application.Sessions.Count.ToString(CultureInfo.InvariantCulture))
            .AppendLine("): ");
        foreach (AudioSession session in application.Sessions.OrderBy(item => item.PipeWireNodeId))
        {
            output.Append("  - Node ")
                .Append(session.PipeWireNodeId.ToString(CultureInfo.InvariantCulture))
                .Append(" | session ")
                .AppendLine(SingleLine(session.Id.Value));
            Append(output, "    Process ID", Redact(session.ProcessId?.ToString(CultureInfo.InvariantCulture), redacted));
            Append(output, "    Application name", session.ApplicationName);
            Append(output, "    PipeWire app ID", session.ApplicationId);
            Append(output, "    Process binary", session.ProcessBinary);
            Append(output, "    Media name", Redact(session.MediaName, redacted));
            Append(output, "    Media role", session.MediaRole);
            Append(output, "    Output device", session.OutputDevice?.Value);
            Append(output, "    Volume",
                string.Concat((session.Volume * 100f).ToString("0.#", CultureInfo.InvariantCulture), "%"));
            Append(output, "    Muted", session.Muted.ToString().ToLowerInvariant());
            Append(output, "    Active", session.Active.ToString().ToLowerInvariant());
        }

        if (redacted)
        {
            output.AppendLine();
            output.AppendLine("Sensitive process paths, process IDs, media names, and process evidence were redacted.");
        }

        return output.ToString();
    }

    private static string? Redact(string? value, bool redacted) =>
        redacted && !string.IsNullOrWhiteSpace(value) ? "(redacted)" : value;

    private static void Append(StringBuilder output, string label, string? value) =>
        output.Append(label).Append(": ").AppendLine(SingleLine(value));

    private static string SingleLine(string? value) => string.IsNullOrWhiteSpace(value)
        ? "(none)"
        : value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
}
