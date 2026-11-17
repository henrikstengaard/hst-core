namespace Hst.Core.Extensions
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public static class ProcessExtensions
    {
        /// <summary>
        /// Run process and wait for it to exit.
        /// </summary>
        /// <param name="command">Command to run.</param>
        /// <param name="args">Arguments for command.</param>
        /// <param name="workingDirectory">Working directory for process.</param>
        /// <param name="successExitCode">Exit code indicating process ran successfully.</param>
        /// <returns>Standard output from process.</returns>
        /// <exception cref="IOException">Process failed to start or exited with an exit code other than success exit code.</exception>
        public static string RunProcess(this string command, string args = null, string workingDirectory = null,
            int successExitCode = 0)
        {
            using (var process = StartProcess(command, args, workingDirectory))
            {
                // read standard error concurrently with standard output to avoid a deadlock, if the process fills
                // the standard error buffer while standard output is being read
                var standardErrorTask = process.StandardError.ReadToEndAsync();
                var standardOutput = process.StandardOutput.ReadToEnd();
                var standardError = standardErrorTask.GetAwaiter().GetResult();

                process.WaitForExit();

                return GetResult(command, args, process.ExitCode, successExitCode, standardOutput, standardError);
            }
        }

        /// <summary>
        /// Run process and wait for it to exit. Process is killed, if cancellation is requested.
        /// </summary>
        /// <param name="command">Command to run.</param>
        /// <param name="args">Arguments for command.</param>
        /// <param name="workingDirectory">Working directory for process.</param>
        /// <param name="successExitCode">Exit code indicating process ran successfully.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Standard output from process.</returns>
        /// <exception cref="IOException">Process failed to start or exited with an exit code other than success exit code.</exception>
        /// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
        public static async Task<string> RunProcessAsync(this string command, string args = null,
            string workingDirectory = null, int successExitCode = 0, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            using (var process = StartProcess(command, args, workingDirectory))
            using (token.Register(() => Kill(process)))
            {
                var standardOutputTask = process.StandardOutput.ReadToEndAsync();
                var standardErrorTask = process.StandardError.ReadToEndAsync();

                var standardOutput = await standardOutputTask.ConfigureAwait(false);
                var standardError = await standardErrorTask.ConfigureAwait(false);

                // streams are closed when the process exits, wait for exit to ensure exit code is available
                process.WaitForExit();

                token.ThrowIfCancellationRequested();

                return GetResult(command, args, process.ExitCode, successExitCode, standardOutput, standardError);
            }
        }

        private static string GetResult(string command, string args, int exitCode, int successExitCode,
            string standardOutput, string standardError)
        {
            if (exitCode != successExitCode)
            {
                throw new IOException(
                    $"Process command '{command}'{FormatArgs(args)} exited with code {exitCode}{Environment.NewLine}{standardOutput}{Environment.NewLine}{standardError}");
            }

            return standardOutput;
        }

        private static void Kill(Process process)
        {
            try
            {
                if (process.HasExited)
                {
                    return;
                }

#if NETCOREAPP3_0_OR_GREATER
                process.Kill(true);
#else
                process.Kill();
#endif
            }
            catch (InvalidOperationException)
            {
                // process has already exited
            }
        }

        private static string FormatArgs(string args) =>
            string.IsNullOrWhiteSpace(args) ? string.Empty : $" with arguments '{args}'";

        private static Process StartProcess(string command, string args = null, string workingDirectory = null)
        {
            Process process;
            try
            {
                process = Process.Start(
                    new ProcessStartInfo(command)
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        Arguments = args ?? string.Empty,
                        WorkingDirectory = workingDirectory ?? string.Empty,
                    });
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                throw new IOException($"Failed to start process command '{command}'{FormatArgs(args)}", e);
            }

            if (process == null)
            {
                throw new IOException($"Failed to start process command '{command}'{FormatArgs(args)}");
            }

            return process;
        }
    }
}
