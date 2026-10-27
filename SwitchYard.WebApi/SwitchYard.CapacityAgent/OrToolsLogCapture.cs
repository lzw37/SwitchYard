using System.Runtime.InteropServices;
using System.Text;
using Serilog;

namespace SwitchYard.CapacityAgent;

/// <summary>
/// Captures the native stdout/stderr used by OR-Tools backends and replays it through Serilog.
/// </summary>
internal static class OrToolsLogCapture
{
    private static readonly object CaptureLock = new();
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "OR-Tools");
    private static readonly ILogger InternalLogger =
        Log.ForContext("SourceContext", typeof(OrToolsLogCapture).FullName ?? nameof(OrToolsLogCapture));

    public static T Run<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        lock (CaptureLock)
        {
            var capturePath = Path.Combine(
                Path.GetTempPath(),
                $"switchyard-ortools-{Environment.ProcessId}-{Guid.NewGuid():N}.log");
            NativeOutputRedirection? redirection = null;

            try
            {
                try
                {
                    redirection = NativeOutputRedirection.Start(capturePath);
                }
                catch (Exception exception)
                {
                    InternalLogger.Warning(
                        exception,
                        "无法捕获 OR-Tools 原生输出；本次求解仍将继续，求解摘要仍会写入日志");
                }

                return operation();
            }
            finally
            {
                redirection?.Dispose();
                ReplayCapturedOutput(capturePath);
                TryDelete(capturePath);
            }
        }
    }

    private static void ReplayCapturedOutput(string capturePath)
    {
        if (!File.Exists(capturePath))
        {
            return;
        }

        try
        {
            foreach (var line in File.ReadLines(capturePath, Encoding.UTF8))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    Logger.Information("{OrToolsMessage}", line.TrimEnd());
                }
            }
        }
        catch (Exception exception)
        {
            InternalLogger.Warning(exception, "读取 OR-Tools 原生日志失败：{CapturePath}", capturePath);
        }
    }

    private static void TryDelete(string capturePath)
    {
        try
        {
            File.Delete(capturePath);
        }
        catch (Exception exception)
        {
            InternalLogger.Debug(exception, "删除 OR-Tools 临时日志失败：{CapturePath}", capturePath);
        }
    }

    private sealed class NativeOutputRedirection : IDisposable
    {
        private readonly bool _windows;
        private readonly int _originalStdOut;
        private readonly int _originalStdErr;
        private bool _disposed;

        private NativeOutputRedirection(bool windows, int originalStdOut, int originalStdErr)
        {
            _windows = windows;
            _originalStdOut = originalStdOut;
            _originalStdErr = originalStdErr;
        }

        public static NativeOutputRedirection Start(string capturePath) =>
            OperatingSystem.IsWindows()
                ? StartWindows(capturePath)
                : StartUnix(capturePath);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_windows)
            {
                WindowsNative.FlushAll();
                RestoreWindowsOutput(_originalStdOut, _originalStdErr);
            }
            else
            {
                UnixNative.FlushAll();
                UnixNative.DuplicateTo(_originalStdOut, 1);
                UnixNative.DuplicateTo(_originalStdErr, 2);
                UnixNative.Close(_originalStdOut);
                UnixNative.Close(_originalStdErr);
            }
        }

        private static NativeOutputRedirection StartWindows(string capturePath)
        {
            WindowsNative.FlushAll();
            var originalStdOut = WindowsNative.Duplicate(1);
            if (originalStdOut < 0)
            {
                throw CreateRedirectException("复制 stdout");
            }

            var originalStdErr = WindowsNative.Duplicate(2);
            if (originalStdErr < 0)
            {
                WindowsNative.Close(originalStdOut);
                throw CreateRedirectException("复制 stderr");
            }

            var capture = WindowsNative.Open(capturePath);
            if (capture < 0)
            {
                WindowsNative.Close(originalStdOut);
                WindowsNative.Close(originalStdErr);
                throw CreateRedirectException("打开临时日志");
            }

            var stdoutRedirected = false;
            try
            {
                if (WindowsNative.DuplicateTo(capture, 1) != 0)
                {
                    throw CreateRedirectException("重定向 stdout");
                }

                stdoutRedirected = true;
                if (WindowsNative.DuplicateTo(capture, 2) != 0)
                {
                    throw CreateRedirectException("重定向 stderr");
                }

                return new NativeOutputRedirection(true, originalStdOut, originalStdErr);
            }
            catch
            {
                if (stdoutRedirected)
                {
                    RestoreWindowsOutput(originalStdOut, originalStdErr);
                }
                else
                {
                    WindowsNative.Close(originalStdOut);
                    WindowsNative.Close(originalStdErr);
                }
                throw;
            }
            finally
            {
                WindowsNative.Close(capture);
            }
        }

        private static NativeOutputRedirection StartUnix(string capturePath)
        {
            UnixNative.FlushAll();
            var originalStdOut = UnixNative.Duplicate(1);
            if (originalStdOut < 0)
            {
                throw CreateRedirectException("复制 stdout");
            }

            var originalStdErr = UnixNative.Duplicate(2);
            if (originalStdErr < 0)
            {
                UnixNative.Close(originalStdOut);
                throw CreateRedirectException("复制 stderr");
            }

            var capture = UnixNative.Open(capturePath);
            if (capture < 0)
            {
                UnixNative.Close(originalStdOut);
                UnixNative.Close(originalStdErr);
                throw CreateRedirectException("打开临时日志");
            }

            var stdoutRedirected = false;
            try
            {
                if (UnixNative.DuplicateTo(capture, 1) < 0)
                {
                    throw CreateRedirectException("重定向 stdout");
                }

                stdoutRedirected = true;
                if (UnixNative.DuplicateTo(capture, 2) < 0)
                {
                    throw CreateRedirectException("重定向 stderr");
                }

                return new NativeOutputRedirection(false, originalStdOut, originalStdErr);
            }
            catch
            {
                if (stdoutRedirected)
                {
                    UnixNative.DuplicateTo(originalStdOut, 1);
                }

                UnixNative.DuplicateTo(originalStdErr, 2);
                UnixNative.Close(originalStdOut);
                UnixNative.Close(originalStdErr);
                throw;
            }
            finally
            {
                UnixNative.Close(capture);
            }
        }

        private static void RestoreWindowsOutput(int originalStdOut, int originalStdErr)
        {
            WindowsNative.DuplicateTo(originalStdOut, 1);
            WindowsNative.DuplicateTo(originalStdErr, 2);
            WindowsNative.SynchronizeStandardHandles();
            WindowsNative.Close(originalStdOut);
            WindowsNative.Close(originalStdErr);

            var outputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), outputEncoding) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError(), outputEncoding) { AutoFlush = true });
        }

        private static InvalidOperationException CreateRedirectException(string operation) =>
            new($"{operation}失败，系统错误码：{Marshal.GetLastPInvokeError()}。");
    }

    private static class WindowsNative
    {
        private const int WriteOnly = 0x0001;
        private const int Create = 0x0100;
        private const int Truncate = 0x0200;
        private const int Binary = 0x8000;
        private const int UserReadWrite = 0x0180;

        [DllImport("ucrtbase.dll", EntryPoint = "_dup", SetLastError = true)]
        private static extern int DuplicateCore(int descriptor);

        [DllImport("ucrtbase.dll", EntryPoint = "_dup2", SetLastError = true)]
        private static extern int DuplicateToCore(int source, int target);

        [DllImport("ucrtbase.dll", EntryPoint = "_close", SetLastError = true)]
        private static extern int CloseCore(int descriptor);

        [DllImport("ucrtbase.dll", EntryPoint = "_wopen", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int OpenCore(string path, int flags, int mode);

        [DllImport("ucrtbase.dll", EntryPoint = "fflush")]
        private static extern int FlushCore(IntPtr stream);

        [DllImport("ucrtbase.dll", EntryPoint = "_get_osfhandle")]
        private static extern IntPtr GetOsFileHandleCore(int descriptor);

        [DllImport("kernel32.dll", EntryPoint = "SetStdHandle", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetStandardHandleCore(int standardHandle, IntPtr handle);

        public static int Duplicate(int descriptor) => DuplicateCore(descriptor);
        public static int DuplicateTo(int source, int target) => DuplicateToCore(source, target);
        public static int Close(int descriptor) => CloseCore(descriptor);
        public static int Open(string path) => OpenCore(path, WriteOnly | Create | Truncate | Binary, UserReadWrite);
        public static void FlushAll() => FlushCore(IntPtr.Zero);
        public static void SynchronizeStandardHandles()
        {
            const int standardOutputHandle = -11;
            const int standardErrorHandle = -12;
            SetStandardHandleCore(standardOutputHandle, GetOsFileHandleCore(1));
            SetStandardHandleCore(standardErrorHandle, GetOsFileHandleCore(2));
        }
    }

    private static class UnixNative
    {
        private const int WriteOnly = 0x0001;
        private const int LinuxCreate = 0x0040;
        private const int LinuxTruncate = 0x0200;
        private const int MacCreate = 0x0200;
        private const int MacTruncate = 0x0400;
        private const int UserReadWrite = 0x0180;

        [DllImport("libc", EntryPoint = "dup", SetLastError = true)]
        private static extern int DuplicateCore(int descriptor);

        [DllImport("libc", EntryPoint = "dup2", SetLastError = true)]
        private static extern int DuplicateToCore(int source, int target);

        [DllImport("libc", EntryPoint = "close", SetLastError = true)]
        private static extern int CloseCore(int descriptor);

        [DllImport("libc", EntryPoint = "open", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int OpenCore(string path, int flags, int mode);

        [DllImport("libc", EntryPoint = "fflush")]
        private static extern int FlushCore(IntPtr stream);

        public static int Duplicate(int descriptor) => DuplicateCore(descriptor);
        public static int DuplicateTo(int source, int target) => DuplicateToCore(source, target);
        public static int Close(int descriptor) => CloseCore(descriptor);
        public static int Open(string path)
        {
            var flags = OperatingSystem.IsMacOS()
                ? WriteOnly | MacCreate | MacTruncate
                : WriteOnly | LinuxCreate | LinuxTruncate;
            return OpenCore(path, flags, UserReadWrite);
        }

        public static void FlushAll() => FlushCore(IntPtr.Zero);
    }
}
