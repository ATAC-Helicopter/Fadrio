using Fadrio.Core;

namespace Fadrio.NativeInterop.Tests;

public sealed class NativeEventTranslatorTests
{
    [Fact]
    public void TranslatesCopiedSessionEventWithoutNativeRuntime()
    {
        var fixture = new NativeEventData(
            NativeEventType.SessionAdded,
            Generation: 3,
            NodeId: 81,
            ProcessId: 1234,
            Volume: 0.52f,
            Muted: false,
            Active: true,
            ApplicationName: "Firefox",
            ApplicationId: "org.mozilla.firefox",
            ApplicationIconName: "firefox",
            ProcessBinary: "firefox",
            MediaName: "AudioStream",
            MediaRole: "Music");

        SessionAdded translated = Assert.IsType<SessionAdded>(NativeEventTranslator.Translate(fixture));
        Assert.Equal(new AudioSessionId("pipewire:3:81"), translated.Session.Id);
        Assert.Equal(1234, translated.Session.ProcessId);
        Assert.Equal("org.mozilla.firefox", translated.Session.ApplicationId);
    }

    [Fact]
    public void TranslatesRemovalUsingGenerationStableRuntimeId()
    {
        SessionRemoved translated = Assert.IsType<SessionRemoved>(NativeEventTranslator.Translate(
            new NativeEventData(NativeEventType.SessionRemoved, 9, NodeId: 4)));
        Assert.Equal(new AudioSessionId("pipewire:9:4"), translated.SessionId);
    }
}
