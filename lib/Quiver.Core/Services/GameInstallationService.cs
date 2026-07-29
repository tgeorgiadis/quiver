using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace Quiver.Core.Services;

public static class GameInstallationService
{
    /// <summary>
    /// True for single-file release binaries: .exe, .appimage, or extensionless (e.g. CrashBandicoot_Linux).
    /// </summary>
    public static bool IsSingleFileExecutableAsset(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return false;

        if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            assetName.EndsWith(".appimage", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !Path.HasExtension(assetName);
    }

    public static async Task InstallOrUpdateGameAsync(
        string downloadPath,
        string gamePath,
        string assetName,
        string version,
        GameInstallationOptions? options = null)
    {
        options ??= GameInstallationOptions.Default;
        Directory.CreateDirectory(gamePath);

        if (IsSingleFileExecutableAsset(assetName))
        {
            var destPath = Path.Combine(gamePath, assetName);
            File.Move(downloadPath, destPath, true);
            MakeExecutableIfNeeded(destPath);
        }
        else if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await ExtractZipAsync(downloadPath, gamePath).ConfigureAwait(false);
        }
        else if (assetName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            await ExtractTarGzAsync(downloadPath, gamePath).ConfigureAwait(false);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported release asset type: '{assetName}'. " +
                "Expected .exe, .appimage, .zip, .tar.gz, or an extensionless binary.");
        }

        try
        {
            EnsureExecutableAtRoot(gamePath, options);
        }
        catch (Exception ex)
        {
            Log(options, $"Warning: EnsureExecutableAtRoot failed: {ex.Message}");
        }

        var versionFile = Path.Combine(gamePath, "version.txt");
        await File.WriteAllTextAsync(versionFile, version).ConfigureAwait(false);
    }

    public static void EnsureExecutableAtRoot(string gamePath, GameInstallationOptions? options = null)
    {
        options ??= GameInstallationOptions.Default;

        if (!Directory.Exists(gamePath))
            return;

        while (!HasTopLevelExecutable(gamePath, options))
        {
            var topLevelDirs = Directory.GetDirectories(gamePath, "*", SearchOption.TopDirectoryOnly);
            var topLevelFiles = Directory.GetFiles(gamePath, "*", SearchOption.TopDirectoryOnly)
                .Where(f => !IsLauncherMetadataFile(f, options))
                .ToList();

            var flattened = false;

            if (topLevelDirs.Length == 1)
            {
                var singleDir = topLevelDirs[0];
                var singleDirCandidates = FindExecutableCandidates(singleDir, SearchOption.AllDirectories, options, out _);

                if (singleDirCandidates.Count > 0)
                {
                    try
                    {
                        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                        Directory.Move(singleDir, tempDir);
                        MoveDirectoryContents(tempDir, gamePath);
                        TryDeleteDirectory(tempDir);

                        Log(options, $"Moved contents from subdirectory to root: {singleDir}");
                        flattened = true;
                    }
                    catch (Exception ex)
                    {
                        Log(options, $"Failed to flatten directory structure: {ex.Message}");
                    }
                }
            }

            if (flattened)
                continue;

            var nestedCandidates = FindExecutableCandidates(gamePath, SearchOption.AllDirectories, options, out _)
                .Where(f => !IsInRootDirectory(f, gamePath))
                .ToList();

            if (nestedCandidates.Count == 0)
                return;

            var candidateFile = nestedCandidates[0];
            var parentDir = Path.GetDirectoryName(candidateFile);

            if (!string.IsNullOrEmpty(parentDir) &&
                topLevelDirs.Contains(parentDir, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                    Directory.Move(parentDir, tempDir);
                    MoveDirectoryContents(tempDir, gamePath);
                    TryDeleteDirectory(tempDir);

                    Log(options, $"Flattened directory containing executable: {parentDir}");
                    continue;
                }
                catch (Exception ex)
                {
                    Log(options, $"Failed to flatten directory: {ex.Message}");
                }
            }

            if (topLevelFiles.Count > 0)
            {
                Log(options, "Leaving wrapper folder structure in place because nested executable cannot be safely flattened.");
            }

            return;
        }
    }

    public static List<string> FindExecutableCandidates(
        string path,
        SearchOption searchOption,
        GameInstallationOptions? options,
        out bool needsWine)
    {
        _ = options;
        needsWine = false;

        if (!Directory.Exists(path))
            return [];

        var executables = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            executables.AddRange(Directory.GetFiles(path, "*.exe", searchOption));
            executables.AddRange(Directory.GetFiles(path, "*.bat", searchOption));
            executables.AddRange(Directory.GetFiles(path, "*.cmd", searchOption));
            executables.AddRange(Directory.GetFiles(path, "launch.bat", searchOption));
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            executables.AddRange(Directory.GetDirectories(path, "*.app", searchOption));
            executables.AddRange(Directory.GetFiles(path, "*", searchOption)
                .Where(IsLikelyExtensionlessExecutable));
        }
        else
        {
            var allFiles = Directory.GetFiles(path, "*", searchOption);

            executables.AddRange(allFiles.Where(f =>
            {
                var fileName = Path.GetFileName(f).ToLowerInvariant();
                return fileName.EndsWith(".x86_64") ||
                       fileName.EndsWith(".appimage") ||
                       fileName.EndsWith(".arm64") ||
                       fileName.EndsWith(".aarch64");
            }));

            executables.AddRange(allFiles.Where(f =>
            {
                var fileName = Path.GetFileName(f).ToLowerInvariant();

                if (fileName.EndsWith(".appimage") || fileName.EndsWith(".x86_64") ||
                    fileName.EndsWith(".arm64") || fileName.EndsWith(".aarch64") ||
                    fileName.EndsWith(".txt") || fileName.EndsWith(".dll") ||
                    fileName.EndsWith(".so") || fileName.EndsWith(".json") ||
                    fileName.EndsWith(".sh") || fileName.EndsWith(".exe"))
                {
                    return false;
                }

                return IsLikelyExtensionlessExecutable(f);
            }));

            if (executables.Count == 0)
            {
                var exeFiles = allFiles.Where(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)).ToList();
                if (exeFiles.Count > 0)
                {
                    executables.AddRange(exeFiles);
                    needsWine = true;
                }
            }

            executables.AddRange(allFiles.Where(f => f.EndsWith(".sh", StringComparison.OrdinalIgnoreCase)));
        }

        return executables
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => GetPathDepth(path, f))
            .ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsLauncherMetadataFile(string path, GameInstallationOptions? options = null)
    {
        options ??= GameInstallationOptions.Default;

        var fileName = Path.GetFileName(path);
        return fileName.Equals("version.txt", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("LastPlayed.txt", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("selected_executable.txt", StringComparison.OrdinalIgnoreCase) ||
               options.AdditionalMetadataFileNames.Any(name => fileName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public static void MoveDirectoryContents(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destDir, relative);

            var destParent = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(destParent))
                Directory.CreateDirectory(destParent);

            File.Move(file, destFile, true);
        }
    }

    static async Task ExtractZipAsync(string downloadPath, string gamePath)
    {
        var tempExtractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempExtractPath);

        try
        {
            ZipFile.ExtractToDirectory(downloadPath, tempExtractPath, overwriteFiles: true);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ExtractNestedZips(tempExtractPath);

                var appBundle = Directory.GetDirectories(tempExtractPath, "*.app", SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(appBundle))
                {
                    var appName = Path.GetFileName(appBundle);
                    var destAppPath = Path.Combine(gamePath, appName);

                    if (Directory.Exists(destAppPath))
                    {
                        Directory.Delete(destAppPath, true);
                    }

                    CopyDirectory(appBundle, destAppPath);
                    return;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var tarGzFile = Directory.GetFiles(tempExtractPath, "*.tar.gz", SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(tarGzFile))
                {
                    await ExtractTarGzAsync(tarGzFile, gamePath).ConfigureAwait(false);
                    return;
                }
            }

            var sourcePath = GetEffectiveExtractionSource(tempExtractPath);
            MoveDirectoryContents(sourcePath, gamePath);
        }
        finally
        {
            TryDeleteDirectory(tempExtractPath);
        }
    }

    static string GetEffectiveExtractionSource(string extractPath)
    {
        var rootDirs = Directory.GetDirectories(extractPath, "*", SearchOption.TopDirectoryOnly);
        var rootFiles = Directory.GetFiles(extractPath, "*", SearchOption.TopDirectoryOnly);

        if (rootDirs.Length == 1 && rootFiles.Length == 0)
        {
            return rootDirs[0];
        }

        return extractPath;
    }

    static void ExtractNestedZips(string tempExtractPath)
    {
        var nestedZips = Directory.GetFiles(tempExtractPath, "*.zip", SearchOption.AllDirectories);
        foreach (var nestedZip in nestedZips)
        {
            var nestedZipDirectory = Path.GetDirectoryName(nestedZip) ?? tempExtractPath;
            var nestedExtractPath = Path.Combine(nestedZipDirectory, Path.GetFileNameWithoutExtension(nestedZip));
            Directory.CreateDirectory(nestedExtractPath);
            ZipFile.ExtractToDirectory(nestedZip, nestedExtractPath, overwriteFiles: true);

            try { File.Delete(nestedZip); } catch { }
        }
    }

    static async Task ExtractTarGzAsync(string sourceFilePath, string destinationDirectoryPath)
    {
        Directory.CreateDirectory(destinationDirectoryPath);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!await TryExtractTarGzWithSystemTarAsync(sourceFilePath, destinationDirectoryPath).ConfigureAwait(false))
                ExtractTarGzManaged(sourceFilePath, destinationDirectoryPath);
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (!await TryExtractTarGzWithSystemTarAsync(sourceFilePath, destinationDirectoryPath).ConfigureAwait(false))
                throw new InvalidOperationException("Could not start tar extraction.");
            return;
        }

        throw new PlatformNotSupportedException("Unsupported operating system for tar.gz extraction");
    }

    /// <summary>
    /// Extracts a .tar.gz using the system <c>tar</c> tool.
    /// Returns false only when tar could not be started (e.g. missing from PATH).
    /// </summary>
    static async Task<bool> TryExtractTarGzWithSystemTarAsync(string sourceFilePath, string destinationDirectoryPath)
    {
        Process? process;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "tar",
                Arguments = $"-xzf \"{sourceFilePath}\" -C \"{destinationDirectoryPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return false;
        }

        if (process is null)
            return false;

        using (process)
        {
            await process.WaitForExitAsync().ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var errorOutput = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"Tar extraction failed: {errorOutput}");
            }
        }

        return true;
    }

    /// <summary>
    /// Managed GZip + ustar extraction used as a Windows fallback when system tar is unavailable.
    /// </summary>
    internal static void ExtractTarGzManaged(string sourceFilePath, string destinationDirectoryPath)
    {
        Directory.CreateDirectory(destinationDirectoryPath);
        using var inputStream = File.OpenRead(sourceFilePath);
        using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress);
        ExtractTarFromStream(gzipStream, destinationDirectoryPath);
    }

    static string GetSafeExtractionPath(string destinationDirectoryPath, string archivePath)
    {
        var sanitizedArchivePath = archivePath.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var fullDestinationRoot = Path.GetFullPath(destinationDirectoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullDestinationPath = Path.GetFullPath(Path.Combine(fullDestinationRoot, sanitizedArchivePath));

        if (!fullDestinationPath.StartsWith(fullDestinationRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !fullDestinationPath.Equals(fullDestinationRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Archive entry escapes the destination directory: {archivePath}");
        }

        return fullDestinationPath;
    }

    internal static void ExtractTarFromStream(Stream tarStream, string destinationDirectoryPath)
    {
        using var reader = new BinaryReader(tarStream);
        var buffer = new byte[8192];

        while (true)
        {
            var headerBytes = reader.ReadBytes(512);
            if (headerBytes.Length < 512) break;

            var fileName = Encoding.ASCII.GetString(headerBytes, 0, 100).Trim('\0', ' ');
            if (string.IsNullOrWhiteSpace(fileName)) break;

            var fileSizeStr = Encoding.ASCII.GetString(headerBytes, 124, 12).Trim('\0', ' ');
            var fileSize = string.IsNullOrEmpty(fileSizeStr) ? 0L : Convert.ToInt64(fileSizeStr, 8);
            var fileType = headerBytes[156];
            var destPath = GetSafeExtractionPath(destinationDirectoryPath, fileName);

            if (fileType == '5')
            {
                Directory.CreateDirectory(destPath);
            }
            else
            {
                var destinationDirectory = Path.GetDirectoryName(destPath);
                if (string.IsNullOrEmpty(destinationDirectory))
                {
                    throw new InvalidDataException($"Invalid archive entry path: {fileName}");
                }

                Directory.CreateDirectory(destinationDirectory);

                using var fileStream = File.Create(destPath);
                var remaining = fileSize;
                while (remaining > 0)
                {
                    var toRead = (int)Math.Min(remaining, buffer.Length);
                    var read = reader.Read(buffer, 0, toRead);
                    if (read == 0)
                        throw new EndOfStreamException($"Unexpected end of tar stream while extracting '{fileName}'.");
                    fileStream.Write(buffer, 0, read);
                    remaining -= read;
                }
            }

            // Content is read exactly; skip only the trailing pad to the next 512-byte boundary.
            var paddingBytes = (int)((512 - (fileSize % 512)) % 512);
            if (paddingBytes > 0)
            {
                reader.ReadBytes(paddingBytes);
            }
        }
    }

    static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }

    static void MakeExecutableIfNeeded(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        try
        {
            var chmodProcess = new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(chmodProcess);
            process?.WaitForExit();
        }
        catch
        {
        }
    }

    static bool HasTopLevelExecutable(string path, GameInstallationOptions options)
    {
        if (!Directory.Exists(path))
            return false;

        return FindExecutableCandidates(path, SearchOption.TopDirectoryOnly, options, out _).Count > 0;
    }

    static bool IsLikelyExtensionlessExecutable(string path)
    {
        try
        {
            return !Path.HasExtension(path) && new FileInfo(path).Length > 1024;
        }
        catch
        {
            return false;
        }
    }

    static int GetPathDepth(string rootPath, string targetPath)
    {
        var relativePath = Path.GetRelativePath(rootPath, targetPath);
        return relativePath.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
    }

    static bool IsInRootDirectory(string path, string rootPath)
    {
        var parentDir = Path.GetDirectoryName(path);
        return parentDir != null &&
               Path.GetFullPath(parentDir).TrimEnd(Path.DirectorySeparatorChar)
                   .Equals(Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar),
                           StringComparison.OrdinalIgnoreCase);
    }

    static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;

        try { Directory.Delete(path, true); } catch { }
    }

    static void Log(GameInstallationOptions options, string message)
    {
        if (options.Log is not null)
        {
            options.Log(message);
        }
        else
        {
            Debug.WriteLine(message);
        }
    }
}
