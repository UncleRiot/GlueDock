using System.IO;
using System.IO.Compression;
using System.Text;

namespace GlueDock;

public static class DebugLog
{
    private const int MinimumActiveLogFileSizeMegabytes = 1;
    private const int MaximumActiveLogFileSizeMegabytes = 10;

    private const int MaximumLogFileCount = 10;

    private static long _maximumActiveLogFileSizeBytes =
        10L * 1024L * 1024L;

    private static readonly object SyncRoot =
        new();

    private static readonly string LogDirectory =
        Path.Combine(
            AppContext.BaseDirectory,
            "GlueDock_Debug");

    private static readonly string ActiveLogFile =
        Path.Combine(
            LogDirectory,
            "GlueDock_DebugLog.log");

    private static bool _enabled;
    private static FileStream? _fileStream;
    private static StreamWriter? _writer;

    public static bool IsEnabled
    {
        get
        {
            lock (SyncRoot)
            {
                return _enabled;
            }
        }
    }

    public static string ActiveLogPath =>
        ActiveLogFile;

    public static void SetMaximumActiveLogFileSizeMegabytes(
        int megabytes)
    {
        lock (SyncRoot)
        {
            int clampedMegabytes =
                Math.Clamp(
                    megabytes,
                    MinimumActiveLogFileSizeMegabytes,
                    MaximumActiveLogFileSizeMegabytes);

            _maximumActiveLogFileSizeBytes =
                clampedMegabytes *
                1024L *
                1024L;

            if (_enabled &&
                _fileStream is not null &&
                _fileStream.Length >=
                _maximumActiveLogFileSizeBytes)
            {
                Rotate();
            }
        }
    }

    public static void SetEnabled(
        bool enabled)
    {
        lock (SyncRoot)
        {
            if (_enabled == enabled)
            {
                return;
            }

            if (!enabled)
            {
                WriteInternal(
                    "Debug",
                    "Debug logging disabled.");

                _enabled = false;
                CloseWriter();
                return;
            }

            _enabled = true;

            try
            {
                OpenWriter();

                WriteInternal(
                    "Debug",
                    "Debug logging enabled.");
            }
            catch
            {
                _enabled = false;
                CloseWriter();
            }
        }
    }

    public static void Write(
        string category,
        string message)
    {
        lock (SyncRoot)
        {
            if (!_enabled)
            {
                return;
            }

            try
            {
                if (_writer is null)
                {
                    OpenWriter();
                }

                WriteInternal(
                    category,
                    message);
            }
            catch
            {
                CloseWriter();
            }
        }
    }

    public static void WriteException(
        string category,
        Exception exception)
    {
        Write(
            category,
            $"{exception.GetType().FullName}: {exception.Message}{Environment.NewLine}{exception.StackTrace}");
    }

    public static void Shutdown()
    {
        lock (SyncRoot)
        {
            if (_enabled)
            {
                WriteInternal(
                    "Debug",
                    "Application logging shutdown.");
            }

            CloseWriter();
            _enabled = false;
        }
    }

    private static void WriteInternal(
        string category,
        string message)
    {
        if (_writer is null ||
            _fileStream is null)
        {
            return;
        }

        string singleLineMessage =
            message
                .Replace(
                    "\r\n",
                    "\\n",
                    StringComparison.Ordinal)
                .Replace(
                    "\n",
                    "\\n",
                    StringComparison.Ordinal)
                .Replace(
                    "\r",
                    "\\n",
                    StringComparison.Ordinal);

        _writer.WriteLine(
            $"{DateTimeOffset.Now:O}\t{Environment.ProcessId}\t{Environment.CurrentManagedThreadId}\t{category}\t{singleLineMessage}");

        _writer.Flush();
        _fileStream.Flush(
            flushToDisk: true);

        if (_fileStream.Length >=
            _maximumActiveLogFileSizeBytes)
        {
            Rotate();
        }
    }

    private static void OpenWriter()
    {
        Directory.CreateDirectory(
            LogDirectory);

        if (File.Exists(
                ActiveLogFile) &&
            new FileInfo(
                ActiveLogFile).Length >=
            _maximumActiveLogFileSizeBytes)
        {
            RotateExistingFiles();
        }

        _fileStream =
            new FileStream(
                ActiveLogFile,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);

        _writer =
            new StreamWriter(
                _fileStream,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                bufferSize: 4096,
                leaveOpen: true)
            {
                AutoFlush = true
            };
    }

    private static void Rotate()
    {
        CloseWriter();
        RotateExistingFiles();

        if (_enabled)
        {
            OpenWriter();

            WriteInternal(
                "Debug",
                $"Log rotated after reaching {_maximumActiveLogFileSizeBytes / (1024L * 1024L)} MiB.");
        }
    }

    private static void RotateExistingFiles()
    {
        Directory.CreateDirectory(
            LogDirectory);

        string oldest =
            GetRotatedLogFile(
                MaximumLogFileCount - 1);

        if (File.Exists(
                oldest))
        {
            File.Delete(
                oldest);
        }

        for (int index =
                 MaximumLogFileCount - 2;
             index >= 1;
             index--)
        {
            string source =
                GetRotatedLogFile(
                    index);

            string destination =
                GetRotatedLogFile(
                    index + 1);

            if (File.Exists(
                    source))
            {
                File.Move(
                    source,
                    destination,
                    overwrite: true);
            }
        }

        if (!File.Exists(
                ActiveLogFile))
        {
            return;
        }

        string rotatedFile =
            GetRotatedLogFile(1);

        CompressLogFile(
            ActiveLogFile,
            rotatedFile);

        File.Delete(
            ActiveLogFile);
    }

    private static string GetRotatedLogFile(
        int index)
    {
        return Path.Combine(
            LogDirectory,
            $"GlueDock_DebugLog.{index}.br");
    }

    private static void CompressLogFile(
        string sourceFile,
        string destinationFile)
    {
        using FileStream source =
            new(
                sourceFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

        using FileStream destination =
            new(
                destinationFile,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);

        using BrotliStream compressor =
            new(
                destination,
                CompressionLevel.Optimal,
                leaveOpen: false);

        source.CopyTo(
            compressor);
    }

    private static void CloseWriter()
    {
        try
        {
            _writer?.Flush();
        }
        catch
        {
        }

        try
        {
            _fileStream?.Flush(
                flushToDisk: true);
        }
        catch
        {
        }

        try
        {
            _writer?.Dispose();
        }
        catch
        {
        }

        try
        {
            _fileStream?.Dispose();
        }
        catch
        {
        }

        _writer = null;
        _fileStream = null;
    }
}
