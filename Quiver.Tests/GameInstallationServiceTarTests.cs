using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Quiver.Core.Services;

namespace Quiver.Tests;

public class GameInstallationServiceTarTests
{
    [Fact]
    public void ExtractTarGzManaged_extracts_multiple_files_with_non_aligned_sizes()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tar.gz");
        var extractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var firstPayload = Encoding.UTF8.GetBytes(new string('A', 100));
            var secondPayload = Encoding.UTF8.GetBytes(new string('B', 250));
            File.WriteAllBytes(archivePath, CreateTarGz(
            [
                ("readme.txt", firstPayload),
                ("data/payload.bin", secondPayload),
            ]));

            GameInstallationService.ExtractTarGzManaged(archivePath, extractPath);

            File.ReadAllBytes(Path.Combine(extractPath, "readme.txt")).Should().Equal(firstPayload);
            File.ReadAllBytes(Path.Combine(extractPath, "data", "payload.bin")).Should().Equal(secondPayload);
        }
        finally
        {
            if (File.Exists(archivePath))
                File.Delete(archivePath);
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);
        }
    }

    [Fact]
    public async Task InstallOrUpdateGameAsync_extracts_tar_gz_asset()
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tar.gz");
        var gamePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            var payload = Encoding.UTF8.GetBytes(new string('C', 100));
            File.WriteAllBytes(archivePath, CreateTarGz([("game.exe", payload)]));

            await GameInstallationService.InstallOrUpdateGameAsync(
                archivePath,
                gamePath,
                "game.tar.gz",
                "v0.8.3");

            File.ReadAllBytes(Path.Combine(gamePath, "game.exe")).Should().Equal(payload);
            File.ReadAllText(Path.Combine(gamePath, "version.txt")).Trim().Should().Be("v0.8.3");
        }
        finally
        {
            if (File.Exists(archivePath))
                File.Delete(archivePath);
            if (Directory.Exists(gamePath))
                Directory.Delete(gamePath, true);
        }
    }

    static byte[] CreateTarGz(IReadOnlyList<(string Path, byte[] Content)> entries)
    {
        using var tarStream = new MemoryStream();
        foreach (var (path, content) in entries)
            WriteUstarEntry(tarStream, path, content);

        // Two zero blocks end a tar archive.
        tarStream.Write(new byte[1024]);

        tarStream.Position = 0;
        using var gzipped = new MemoryStream();
        using (var gzip = new GZipStream(gzipped, CompressionLevel.Optimal, leaveOpen: true))
            tarStream.CopyTo(gzip);

        return gzipped.ToArray();
    }

    static void WriteUstarEntry(Stream tarStream, string path, byte[] content)
    {
        var header = new byte[512];
        var normalizedPath = path.Replace('\\', '/');
        Encoding.ASCII.GetBytes(normalizedPath).AsSpan().CopyTo(header.AsSpan(0, Math.Min(normalizedPath.Length, 100)));

        WriteOctalField(header, 100, 8, Convert.ToInt64("644", 8));
        WriteOctalField(header, 108, 8, 0);
        WriteOctalField(header, 116, 8, 0);
        WriteOctalField(header, 124, 12, content.Length);
        WriteOctalField(header, 136, 12, 0);
        // Checksum field is spaces while computing the checksum.
        for (var i = 148; i < 156; i++)
            header[i] = (byte)' ';
        header[156] = (byte)'0';
        Encoding.ASCII.GetBytes("ustar").CopyTo(header, 257);
        header[262] = 0;
        Encoding.ASCII.GetBytes("00").CopyTo(header, 263);

        var checksum = 0;
        foreach (var b in header)
            checksum += b;
        WriteOctalField(header, 148, 8, checksum);

        tarStream.Write(header);
        tarStream.Write(content);

        var padding = (512 - (content.Length % 512)) % 512;
        if (padding > 0)
            tarStream.Write(new byte[padding]);
    }

    static void WriteOctalField(byte[] header, int offset, int length, long value)
    {
        var formatted = Convert.ToString(value, 8).PadLeft(length - 1, '0');
        if (formatted.Length > length - 1)
            formatted = formatted[^(length - 1)..];

        Encoding.ASCII.GetBytes(formatted).CopyTo(header, offset);
        header[offset + length - 1] = 0;
    }
}
