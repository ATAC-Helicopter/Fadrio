using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Fadrio.Core;

namespace Fadrio.NativeInterop;

public sealed class NativeAudioBackend : IAudioBackend
{
    private readonly Channel<AudioBackendEvent> _events = Channel.CreateUnbounded<AudioBackendEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
    private readonly NativeMethods.EventCallback _callback;
    private IntPtr _context;
    private bool _started;
    private bool _disposed;

    public NativeAudioBackend()
    {
        uint actualVersion = NativeMethods.vm_get_abi_version();
        if (actualVersion != NativeMethods.ExpectedAbiVersion)
        {
            throw new InvalidOperationException(
                $"Native ABI mismatch: expected {NativeMethods.ExpectedAbiVersion}, found {actualVersion}.");
        }

        _callback = OnNativeEvent;
        _context = NativeMethods.vm_context_create(_callback, IntPtr.Zero);
        if (_context == IntPtr.Zero)
        {
            throw new InvalidOperationException("The native PipeWire context could not be allocated.");
        }
    }

    public async IAsyncEnumerable<AudioBackendEvent> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Start();
        await foreach (AudioBackendEvent backendEvent in _events.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return backendEvent;
        }
    }

    public ValueTask SetStreamVolumeAsync(uint nodeId, float volume, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThan(volume, 0f);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(volume, 1f);
        ThrowOnNativeError(NativeMethods.vm_set_stream_volume(_context, nodeId, volume), "set stream volume");
        return ValueTask.CompletedTask;
    }

    public ValueTask SetStreamMuteAsync(uint nodeId, bool muted, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowOnNativeError(NativeMethods.vm_set_stream_mute(_context, nodeId, muted ? (byte)1 : (byte)0), "set stream mute");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_context != IntPtr.Zero)
            {
                NativeMethods.vm_stop(_context);
                NativeMethods.vm_context_destroy(_context);
                _context = IntPtr.Zero;
            }
            _events.Writer.TryComplete();
            GC.KeepAlive(_callback);
        }
        return ValueTask.CompletedTask;
    }

    private void Start()
    {
        if (_started)
        {
            return;
        }
        ThrowOnNativeError(NativeMethods.vm_start(_context), "connect to PipeWire");
        _started = true;
    }

    private void OnNativeEvent(IntPtr nativeEventPointer, IntPtr userData)
    {
        _ = userData;
        try
        {
            NativeMethods.Event native = Marshal.PtrToStructure<NativeMethods.Event>(nativeEventPointer);
            NativeMethods.SessionInfo session = native.Session;
            var copied = new NativeEventData(
                native.Type,
                checked((long)native.Generation),
                session.NodeId,
                session.ProcessId,
                session.Volume,
                session.Muted != 0,
                session.Active != 0,
                Utf8(session.ApplicationName),
                Utf8(session.ApplicationId),
                Utf8(session.ApplicationIconName),
                Utf8(session.ProcessBinary),
                Utf8(session.MediaName),
                Utf8(session.MediaRole),
                Utf8(native.Message));
            _events.Writer.TryWrite(NativeEventTranslator.Translate(copied));
        }
        catch (Exception exception)
        {
            _events.Writer.TryComplete(exception);
        }
    }

    private static string? Utf8(IntPtr pointer) =>
        pointer == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(pointer);

    private static void ThrowOnNativeError(int result, string operation)
    {
        if (result < 0)
        {
            throw new InvalidOperationException($"Native backend could not {operation} (error {result}).");
        }
    }
}
