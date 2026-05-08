using Logix.Driver;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TcHmiSrv.Core;

namespace TcHmiLogixDriver.Logix
{
    record TargetDiagnostics(bool isConnected = false, string controllerInfo = "");
    class LogixDriverDiagnostics
    {
        public Dictionary<string, TargetDiagnostics> Targets { get; } = new();

        public Value ToValue()
        {
            var targets = new Value();

            foreach (var target in Targets) 
            {
                var targetValue = new Value();
                targetValue.Add("isConnected", target.Value.isConnected);
                targetValue.Add("controllerInfo", target.Value.controllerInfo);

                targets.Add(target.Key, targetValue);
            }
            var root = new Value();
            root.Add("Targets", targets);
            
            return root;
        }


        private static readonly object reconnectingDriversLock = new();
        private static readonly Dictionary<string, DriverReconnector> reconnectingDrivers = new();
        public static void TryConnectDriver(IDriver driver)
        {
            // enforce single reconnector per driver
            lock (reconnectingDriversLock)
            {
                if (reconnectingDrivers.ContainsKey(driver.Target.Name))
                    return;

                var reconnector = new DriverReconnector(driver, () =>
                {
                    lock (reconnectingDriversLock)
                        reconnectingDrivers.Remove(driver.Target.Name);
                });
                reconnectingDrivers.Add(driver.Target.Name, reconnector);
            }
        }
    }

    class DriverReconnector
    {
        private static readonly TimeSpan[] backoff =
        {
            TimeSpan.FromMilliseconds(2500),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
        };

        private readonly CancellationTokenSource cts = new();

        public DriverReconnector(IDriver driver, Action onSuccess)
        {
            _ = Task.Run(() => RunAsync(driver, onSuccess, cts.Token));
        }

        private async Task RunAsync(IDriver driver, Action onSuccess, CancellationToken cancel)
        {
            int attempt = 0;
            while (!cancel.IsCancellationRequested)
            {
                var delay = backoff[Math.Min(attempt, backoff.Length - 1)];
                try
                {
                    await Task.Delay(delay, cancel);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    if (driver.TryConnect())
                    {
                        onSuccess();
                        return;
                    }
                }
                catch
                {
                    // swallow and keep retrying
                }
                attempt++;
            }
        }
    }
    
}