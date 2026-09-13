using Fadrio.Application;
using Fadrio.Infrastructure;
using Fadrio.NativeInterop;
using Fadrio.Platform.Linux;

string command = args.FirstOrDefault() ?? "apps";
if (command is not ("apps" or "set" or "mute" or "unmute"))
{
    PrintUsage();
    return 2;
}

bool watch = command == "apps" && args.Contains("--watch", StringComparer.Ordinal);
if ((command == "set" && args.Length != 3) ||
    (command is "mute" or "unmute" && args.Length != 2))
{
    PrintUsage();
    return 2;
}

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};
try
{
    await using var backend = new ReconnectingAudioBackend(() => new NativeAudioBackend());
    var desktopIndex = new XdgDesktopApplicationIndex();
    var resolver = new ApplicationResolver(new LinuxProcMetadataProvider(), desktopIndex,
        new SteamApplicationResolver(new SteamApplicationIndex()));
    var coordinator = new MixerStateCoordinator(resolver);
    if (watch)
    {
        coordinator.SnapshotChanged += (_, snapshot) => Print(snapshot);
    }
    Task coordinatorTask = coordinator.RunAsync(backend.WatchAsync(shutdown.Token), shutdown.Token);
    if (watch)
    {
        await coordinatorTask;
    }
    else
    {
        await Task.Delay(TimeSpan.FromSeconds(2), shutdown.Token);
    }

    if (command == "apps" && !watch)
    {
        Print(coordinator.Current);
    }
    else if (command != "apps")
    {
        var mixerCommands = new MixerCommands(backend, coordinator);
        var applicationId = new Fadrio.Core.ApplicationId(args[1]);
        if (coordinator.Current.Applications.All(application => application.Identity.Id != applicationId))
        {
            Console.Error.WriteLine($"Application '{applicationId}' is not currently available.");
            shutdown.Cancel();
            await IgnoreCancellationAsync(coordinatorTask);
            return 1;
        }
        if (command == "set")
        {
            if (!float.TryParse(args[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float percentage) ||
                percentage is < 0f or > 100f)
            {
                Console.Error.WriteLine("Volume must be a number from 0 to 100.");
                return 2;
            }
            await mixerCommands.SetApplicationVolumeAsync(applicationId, percentage / 100f, shutdown.Token);
            Console.WriteLine($"Set {applicationId} to {percentage:0.#}% across all current sessions.");
        }
        else
        {
            bool muted = command == "mute";
            await mixerCommands.SetApplicationMuteAsync(applicationId, muted, shutdown.Token);
            Console.WriteLine($"{(muted ? "Muted" : "Unmuted")} {applicationId} across all current sessions.");
        }
    }
    shutdown.Cancel();
    await IgnoreCancellationAsync(coordinatorTask);
    return 0;
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    return 0;
}
catch (DllNotFoundException)
{
    Console.Error.WriteLine("libfadrio_native.so was not found. Run ./scripts/build.sh first.");
    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  fadrioctl apps [--watch]");
    Console.Error.WriteLine("  fadrioctl set <canonical-id> <0-100>");
    Console.Error.WriteLine("  fadrioctl mute <canonical-id>");
    Console.Error.WriteLine("  fadrioctl unmute <canonical-id>");
}

static async Task IgnoreCancellationAsync(Task task)
{
    try
    {
        await task;
    }
    catch (OperationCanceledException)
    {
        // Normal CLI shutdown.
    }
}

static void Print(Fadrio.Core.MixerSnapshot snapshot)
{
    if (!Console.IsOutputRedirected)
    {
        Console.Clear();
    }
    if (snapshot.Applications.Count == 0)
    {
        Console.WriteLine("No applications are currently playing audio.");
        return;
    }

    foreach (Fadrio.Core.RuntimeApplication application in snapshot.Applications)
    {
        Console.WriteLine($"Application: {application.Identity.DisplayName}");
        Console.WriteLine($"Canonical ID: {application.Identity.Id}");
        Console.WriteLine($"Confidence: {application.Identity.Confidence}");
        Console.WriteLine($"Sessions: {application.Sessions.Count}");
        Console.WriteLine($"Processes: {string.Join(", ", application.Sessions.Select(session => session.ProcessId).Where(id => id.HasValue).Select(id => id!.Value))}");
        string percentage = (application.EffectiveVolume * 100f).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        Console.WriteLine($"Volume: {percentage}%{(application.IsMixedVolume ? " (mixed)" : string.Empty)}");
        Console.WriteLine($"Muted: {application.IsMuted.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Icon: {application.Identity.Icon?.Value ?? "(none)"}");
        Console.WriteLine();
    }
}
