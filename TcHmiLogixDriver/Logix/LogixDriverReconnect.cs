using Logix.Driver;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TcHmiLogixDriver.Logix
{
    public sealed class LogixDriverReconnect : IDisposable
    {
        private static readonly TimeSpan[] backoff =
        {
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
        };

        private readonly IDriver driver;
        private readonly SemaphoreSlim reconnectGate = new(1, 1);
        private readonly CancellationTokenSource shutdownCts = new();

        private Task? reconnectTask;
        private bool disposed;

        public LogixDriverReconnect(IDriver driver)
        {
            this.driver = driver;
        }

        /// <summary>
        /// Starts a reconnect loop if one is not already running.
        /// Duplicate calls while reconnecting are ignored.
        /// </summary>
        public void RequestReconnect()
        {
            ThrowIfDisposed();

            if (!reconnectGate.Wait(0))
                return;
            
            reconnectTask = RunReconnectLoopAsync();
        }

        private async Task RunReconnectLoopAsync()
        {
            try
            {
                int attempt = 0;

                while (!shutdownCts.IsCancellationRequested)
                {
                    var delay = backoff[Math.Min(attempt, backoff.Length - 1)];

                    try
                    {
                        await Task.Delay(delay, shutdownCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    try
                    {
                        if (await driver.TryConnectAsync(shutdownCts.Token))
                            return;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch
                    {
                        // optional: log exception
                    }

                    attempt++;
                }
            }
            finally
            {
                reconnectTask = null;
                reconnectGate.Release();
            }
        }

        public async Task StopAsync()
        {
            if (disposed)
                return;

            await shutdownCts.CancelAsync();

            Task? task = reconnectTask;

            if (task != null)
            {
                try
                {
                    await task;
                }
                catch
                {
                    // swallow shutdown exceptions
                }
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            shutdownCts.Cancel();

            reconnectGate.Dispose();
            shutdownCts.Dispose();
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }
}
