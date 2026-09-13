using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Fadrio.Core;
using Fadrio.Infrastructure;

namespace Fadrio.Infrastructure.Tests;

public sealed class LocalIconResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fadrio-icon-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task ResolvesNamedPngFromTrustedThemeAndCachesIt()
    {
        string icons = CreateDirectory("icons");
        string source = Path.Combine(CreateDirectory("icons/hicolor/48x48/apps"), "fixture-player.png");
        WritePng(source, 48, 48);
        var resolver = CreateResolver(icons);

        var result = await resolver.ResolveAsync(
            new IconReference("fixture-player"), TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("image/png", result.MediaType);
        Assert.Equal(48, result.Width);
        Assert.Equal(48, result.Height);
        Assert.StartsWith(Path.Combine(_root, "cache"), result.Path, StringComparison.Ordinal);
        Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(result.Path));
    }

    [Fact]
    public async Task ReusesCacheUntilSourceModificationSignatureChanges()
    {
        string icons = CreateDirectory("icons");
        string source = Path.Combine(icons, "fixture.png");
        WritePng(source, 32, 32);
        var resolver = CreateResolver(icons);

        var first = await resolver.ResolveAsync(new IconReference(source), TestContext.Current.CancellationToken);
        var second = await resolver.ResolveAsync(new IconReference(source), TestContext.Current.CancellationToken);
        WritePng(source, 64, 64);
        File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddSeconds(2));
        var modified = await resolver.ResolveAsync(new IconReference(source), TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.Equal(first.Path, second!.Path);
        Assert.NotEqual(first.Path, modified!.Path);
        Assert.Equal(64, modified.Width);
    }

    [Theory]
    [InlineData("https://example.invalid/icon.png")]
    [InlineData("../icon.png")]
    [InlineData("fixture.svg")]
    public async Task RejectsRemoteTraversalAndUnsupportedReferences(string reference)
    {
        string icons = CreateDirectory("icons");
        var resolver = CreateResolver(icons);

        Assert.Null(await resolver.ResolveAsync(
            new IconReference(reference), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsAbsolutePathOutsideTrustedDirectories()
    {
        string icons = CreateDirectory("icons");
        string outside = Path.Combine(CreateDirectory("outside"), "icon.png");
        WritePng(outside, 16, 16);

        Assert.Null(await CreateResolver(icons).ResolveAsync(
            new IconReference(outside), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsSymlinkThatEscapesTrustedDirectories()
    {
        string icons = CreateDirectory("icons");
        string outside = Path.Combine(CreateDirectory("outside"), "icon.png");
        WritePng(outside, 16, 16);
        string link = Path.Combine(icons, "linked.png");
        File.CreateSymbolicLink(link, outside);

        Assert.Null(await CreateResolver(icons).ResolveAsync(
            new IconReference(link), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsOversizedDimensionsAndFiles()
    {
        string icons = CreateDirectory("icons");
        string malformed = Path.Combine(icons, "malformed.png");
        File.WriteAllBytes(malformed, new byte[32]);
        string hugeDimensions = Path.Combine(icons, "huge.png");
        WritePngHeader(hugeDimensions, 4097, 1);
        string hugeFile = Path.Combine(icons, "large.png");
        WritePngHeader(hugeFile, 1, 1, totalBytes: 65);
        var resolver = CreateResolver(icons, new LocalIconResolverOptions
        {
            IconDirectories = [icons],
            CacheDirectory = Path.Combine(_root, "cache"),
            MaxSourceBytes = 64,
            MaxCacheBytes = 128
        });

        Assert.Null(await resolver.ResolveAsync(
            new IconReference(malformed), TestContext.Current.CancellationToken));
        Assert.Null(await resolver.ResolveAsync(
            new IconReference(hugeDimensions), TestContext.Current.CancellationToken));
        Assert.Null(await resolver.ResolveAsync(
            new IconReference(hugeFile), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PrunesOldEntriesToConfiguredBound()
    {
        string icons = CreateDirectory("icons");
        string firstSource = Path.Combine(icons, "first.png");
        string secondSource = Path.Combine(icons, "second.png");
        WritePng(firstSource, 16, 16);
        WritePng(secondSource, 32, 32);
        var resolver = CreateResolver(icons, new LocalIconResolverOptions
        {
            IconDirectories = [icons],
            CacheDirectory = Path.Combine(_root, "cache"),
            MaxSourceBytes = 1024,
            MaxCacheBytes = 1024,
            MaxCacheEntries = 1
        });

        var first = await resolver.ResolveAsync(
            new IconReference(firstSource), TestContext.Current.CancellationToken);
        var second = await resolver.ResolveAsync(
            new IconReference(secondSource), TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.False(File.Exists(first.Path));
        Assert.True(File.Exists(second.Path));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(_root, "cache"), "*.png"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private LocalIconResolver CreateResolver(string icons, LocalIconResolverOptions? options = null) =>
        new(options ?? new LocalIconResolverOptions
        {
            IconDirectories = [icons],
            CacheDirectory = Path.Combine(_root, "cache")
        });

    private string CreateDirectory(string relativePath)
    {
        string path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WritePng(string path, uint width, uint height)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);

        byte[] header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(output, "IHDR", header);

        using var pixels = new MemoryStream();
        byte[] row = new byte[checked((int)(width * 4 + 1))];
        for (uint index = 0; index < height; index++)
        {
            pixels.Write(row);
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(pixels.GetBuffer().AsSpan(0, checked((int)pixels.Length)));
        }

        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        File.WriteAllBytes(path, output.ToArray());
    }

    private static void WritePngHeader(string path, uint width, uint height, int totalBytes = 32)
    {
        byte[] bytes = new byte[totalBytes];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12, 4));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20, 4), height);
        File.WriteAllBytes(path, bytes);
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> value = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(value, checked((uint)data.Length));
        output.Write(value);
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        byte[] crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput.AsSpan(typeBytes.Length));
        BinaryPrimitives.WriteUInt32BigEndian(value, CalculateCrc32(crcInput));
        output.Write(value);
    }

    private static uint CalculateCrc32(ReadOnlySpan<byte> data)
    {
        uint crc = uint.MaxValue;
        foreach (byte value in data)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
            }
        }

        return ~crc;
    }
}
