namespace Hst.Core.Tests.ExtensionTests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensions;
    using Xunit;

    public class GivenProcessExtensions
    {
        // more than the pipe buffer size on all platforms (typically 4-64 KB)
        private const int LargeOutputSize = 200000;

        private static (string Command, string Args) Shell(string windowsScript, string unixScript) =>
            OperatingSystem.IsWindows()
                ? ("powershell", $"-NoProfile -NonInteractive -Command \"{windowsScript}\"")
                : ("sh", $"-c \"{unixScript}\"");

        private static (string Command, string Args) EchoHello() =>
            Shell("Write-Output 'hello'", "echo hello");

        private static (string Command, string Args) ExitWithCode(int exitCode) =>
            Shell($"[Console]::Error.Write('failed'); exit {exitCode}", $"echo failed >&2; exit {exitCode}");

        private static (string Command, string Args) LargeStandardErrorOutput() =>
            Shell($"[Console]::Error.Write('x' * {LargeOutputSize}); Write-Output 'done'",
                $"head -c {LargeOutputSize} /dev/zero | tr '\\\\0' x >&2; echo done");

        private static (string Command, string Args) Sleep(int seconds) =>
            Shell($"Start-Sleep -Seconds {seconds}", $"sleep {seconds}");

        [Fact]
        public void WhenRunningProcessThenStandardOutputIsReturned()
        {
            var (command, args) = EchoHello();

            var output = command.RunProcess(args);

            Assert.Equal("hello", output.Trim());
        }

        [Fact]
        public async Task WhenRunningProcessAsyncThenStandardOutputIsReturned()
        {
            var (command, args) = EchoHello();

            var output = await command.RunProcessAsync(args);

            Assert.Equal("hello", output.Trim());
        }

        [Fact]
        public void WhenRunningProcessExitingWithErrorCodeThenExceptionIsThrown()
        {
            var (command, args) = ExitWithCode(3);

            var exception = Assert.Throws<IOException>(() => command.RunProcess(args));

            Assert.Contains("exited with code 3", exception.Message);
            Assert.Contains("failed", exception.Message);
        }

        [Fact]
        public async Task WhenRunningProcessAsyncExitingWithErrorCodeThenExceptionIsThrown()
        {
            var (command, args) = ExitWithCode(3);

            var exception = await Assert.ThrowsAsync<IOException>(() => command.RunProcessAsync(args));

            Assert.Contains("exited with code 3", exception.Message);
        }

        [Fact]
        public void WhenRunningProcessWithSuccessExitCodeThenNoExceptionIsThrown()
        {
            var (command, args) = ExitWithCode(3);

            command.RunProcess(args, successExitCode: 3);
        }

        [Fact]
        public async Task WhenRunningProcessWritingLargeStandardErrorThenProcessDoesNotDeadlock()
        {
            var (command, args) = LargeStandardErrorOutput();

            var task = Task.Run(() => command.RunProcess(args));

            Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30))));
            Assert.Equal("done", (await task).Trim());
        }

        [Fact]
        public async Task WhenRunningProcessAsyncWritingLargeStandardErrorThenProcessDoesNotDeadlock()
        {
            var (command, args) = LargeStandardErrorOutput();

            var task = command.RunProcessAsync(args);

            Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30))));
            Assert.Equal("done", (await task).Trim());
        }

        [Fact]
        public async Task WhenCancellingRunProcessAsyncThenProcessIsKilledAndOperationIsCancelled()
        {
            var (command, args) = Sleep(30);
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            var stopwatch = Stopwatch.StartNew();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                command.RunProcessAsync(args, token: cancellationTokenSource.Token));

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), "Process was not killed on cancellation");
        }

        [Fact]
        public void WhenRunningNonExistingCommandThenExceptionIsThrown()
        {
            Assert.Throws<IOException>(() => "hst-non-existing-command".RunProcess());
        }
    }
}
