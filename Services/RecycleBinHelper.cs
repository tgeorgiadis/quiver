using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Quiver.Services;

/// <summary>
/// Moves files/directories to the OS Recycle Bin / Trash instead of permanent deletion.
/// </summary>
public static class RecycleBinHelper
{
    public static void MoveToRecycleBin(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            throw new FileNotFoundException("Path does not exist.", fullPath);

        if (OperatingSystem.IsWindows())
            MoveToRecycleBinWindows(fullPath);
        else if (OperatingSystem.IsLinux())
            MoveToRecycleBinLinux(fullPath);
        else if (OperatingSystem.IsMacOS())
            MoveToRecycleBinMacOs(fullPath);
        else
            throw new PlatformNotSupportedException("Moving to Recycle Bin / Trash is not supported on this platform.");
    }

    [SupportedOSPlatform("windows")]
    private static void MoveToRecycleBinWindows(string fullPath)
    {
        // Double-null-terminated path list required by SHFileOperationW.
        var fromBytes = Encoding.Unicode.GetBytes(fullPath + "\0\0");
        var fromPtr = Marshal.AllocHGlobal(fromBytes.Length);
        try
        {
            Marshal.Copy(fromBytes, 0, fromPtr, fromBytes.Length);

            var fileOp = new SHFILEOPSTRUCT
            {
                hwnd = IntPtr.Zero,
                wFunc = FO_DELETE,
                pFrom = fromPtr,
                pTo = IntPtr.Zero,
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT,
                fAnyOperationsAborted = false,
                hNameMappings = IntPtr.Zero,
                lpszProgressTitle = IntPtr.Zero,
            };

            var result = SHFileOperationW(ref fileOp);
            if (result != 0 || fileOp.fAnyOperationsAborted)
                throw new IOException($"Failed to move '{fullPath}' to the Recycle Bin (error {result}).");
        }
        finally
        {
            Marshal.FreeHGlobal(fromPtr);
        }

        if (File.Exists(fullPath) || Directory.Exists(fullPath))
            throw new IOException($"Failed to move '{fullPath}' to the Recycle Bin.");
    }

    internal static string BuildLinuxTrashInfo(string originalPath, DateTime deletionTimeUtc)
    {
        var escapedPath = Uri.EscapeDataString(originalPath).Replace("%2F", "/", StringComparison.Ordinal);
        var timestamp = deletionTimeUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss");
        return $"[Trash Info]\nPath={escapedPath}\nDeletionDate={timestamp}\n";
    }

    internal static string AllocateUniqueTrashName(string trashFilesDir, string trashInfoDir, string baseName)
    {
        var candidate = baseName;
        var counter = 1;
        while (File.Exists(Path.Combine(trashFilesDir, candidate)) ||
               Directory.Exists(Path.Combine(trashFilesDir, candidate)) ||
               File.Exists(Path.Combine(trashInfoDir, candidate + ".trashinfo")))
        {
            candidate = $"{baseName}.{counter}";
            counter++;
        }

        return candidate;
    }

    [SupportedOSPlatform("linux")]
    private static void MoveToRecycleBinLinux(string fullPath)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            throw new InvalidOperationException("Could not resolve the user home directory for Trash.");

        var trashRoot = Path.Combine(home, ".local", "share", "Trash");
        var trashFiles = Path.Combine(trashRoot, "files");
        var trashInfo = Path.Combine(trashRoot, "info");
        Directory.CreateDirectory(trashFiles);
        Directory.CreateDirectory(trashInfo);

        var baseName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(baseName))
            baseName = "deleted-item";

        var trashName = AllocateUniqueTrashName(trashFiles, trashInfo, baseName);
        var destination = Path.Combine(trashFiles, trashName);
        var infoPath = Path.Combine(trashInfo, trashName + ".trashinfo");

        File.WriteAllText(infoPath, BuildLinuxTrashInfo(fullPath, DateTime.UtcNow), Encoding.UTF8);

        try
        {
            if (Directory.Exists(fullPath))
                Directory.Move(fullPath, destination);
            else
                File.Move(fullPath, destination);
        }
        catch
        {
            try { File.Delete(infoPath); } catch { /* best effort */ }
            throw;
        }

        if (File.Exists(fullPath) || Directory.Exists(fullPath))
            throw new IOException($"Failed to move '{fullPath}' to Trash.");
    }

    [SupportedOSPlatform("macos")]
    private static void MoveToRecycleBinMacOs(string fullPath)
    {
        // Finder "delete" moves to Trash (recoverable), unlike rm.
        var escaped = fullPath.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        var script = $"tell application \"Finder\" to delete (POSIX file \"{escaped}\" as alias)";

        var startInfo = new ProcessStartInfo
        {
            FileName = "osascript",
            ArgumentList = { "-e", script },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new IOException("Failed to start osascript to move the item to Trash.");

        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(60_000);
        if (process.ExitCode != 0)
            throw new IOException($"Failed to move '{fullPath}' to Trash: {stderr.Trim()}");

        if (File.Exists(fullPath) || Directory.Exists(fullPath))
            throw new IOException($"Failed to move '{fullPath}' to Trash.");
    }

    private const int FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_NOERRORUI = 0x0400;
    private const ushort FOF_SILENT = 0x0004;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public int wFunc;
        public IntPtr pFrom;
        public IntPtr pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public IntPtr lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperationW(ref SHFILEOPSTRUCT fileOp);
}
