using Fadrio.Core;

namespace Fadrio.Cli.Tests;

public sealed class DiagnosticTextRendererTests
{
    [Fact]
    public void RenderIncludesIdentityEvidenceAndOrderedSessionDetails()
    {
        string output = DiagnosticTextRenderer.Render(Application(), redacted: false);

        Assert.Contains("Canonical ID: xdg:org.mozilla.firefox", output, StringComparison.Ordinal);
        Assert.Contains("Executable: /usr/lib/firefox/firefox", output, StringComparison.Ordinal);
        Assert.Contains("DesktopEntry: org.mozilla.firefox — Exact desktop match.", output, StringComparison.Ordinal);
        Assert.Contains("ProcessExecutable: /usr/lib/firefox/firefox", output, StringComparison.Ordinal);
        Assert.True(output.IndexOf("Node 81", StringComparison.Ordinal) < output.IndexOf("Node 94", StringComparison.Ordinal));
        Assert.Contains("Media name: YouTube", output, StringComparison.Ordinal);
        Assert.Contains("Volume: 52%", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RedactedRenderHidesSensitiveProcessAndMediaValues()
    {
        string output = DiagnosticTextRenderer.Render(Application(), redacted: true);

        Assert.DoesNotContain("/usr/lib/firefox/firefox", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Process ID: 4242", output, StringComparison.Ordinal);
        Assert.DoesNotContain("YouTube", output, StringComparison.Ordinal);
        Assert.Contains("Process ID: (redacted)", output, StringComparison.Ordinal);
        Assert.Contains("Media name: (redacted)", output, StringComparison.Ordinal);
        Assert.Contains("process evidence were redacted", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderFlattensControlCharactersFromExternalMetadata()
    {
        RuntimeApplication application = Application("Firefox\nforged heading");

        string output = DiagnosticTextRenderer.Render(application, redacted: false);

        Assert.Contains("Application: Firefox forged heading", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Firefox\nforged", output, StringComparison.Ordinal);
    }

    private static RuntimeApplication Application(string displayName = "Firefox") => new(
        new ApplicationIdentity
        {
            Id = new("xdg:org.mozilla.firefox"),
            DisplayName = displayName,
            DesktopFileId = "org.mozilla.firefox",
            ExecutablePath = "/usr/lib/firefox/firefox",
            ExecutableName = "firefox",
            Icon = new("/usr/share/icons/firefox.png"),
            Confidence = IdentityConfidence.High,
            Evidence =
            [
                new(IdentityEvidenceKind.DesktopEntry, "org.mozilla.firefox", "Exact desktop match."),
                new(IdentityEvidenceKind.ProcessExecutable, "/usr/lib/firefox/firefox", "Resolved from proc.")
            ]
        },
        [
            Session("second", 94, 5151, "WebRTC"),
            Session("first", 81, 4242, "YouTube")
        ]);

    private static AudioSession Session(string id, uint node, int process, string media) => new()
    {
        Id = new(id),
        PipeWireNodeId = node,
        ProcessId = process,
        ApplicationName = "Firefox",
        ApplicationId = "org.mozilla.firefox",
        ProcessBinary = "firefox",
        MediaName = media,
        MediaRole = "Music",
        OutputDevice = new("speakers"),
        Volume = 0.52f,
        Active = true
    };
}
