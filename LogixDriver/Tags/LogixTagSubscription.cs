using libplctag;
using Logix;
using System;
using System.Collections.Concurrent;

namespace Logix.Tags
{
    public class LogixTagSubscription : IDisposable
    {
        public bool Busy => isSubscriptionProcessing == 1;

        private ConcurrentDictionary<string, SubscribedTag> subscribedTags = new();
        private PeriodicTimer subscriptionTimer;
        private Task? subscriptionProcess;
        private CancellationTokenSource? subscriptionCts;
        private readonly LogixDriver driver;

        private int isSubscriptionProcessing;
        private TaskCompletionSource idleTask = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public LogixTagSubscription(LogixDriver driver, uint rateMs = 500)
        {
            this.driver = driver;
            subscriptionTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(rateMs));
        }

        public void SubscribeTag(string tagName, Tag tag)
        {
            if (subscriptionProcess is null)
            {
                subscriptionCts = new CancellationTokenSource();
                subscriptionProcess = Task.Run(() => ProcessSubscriptionReads(subscriptionCts.Token));
            }

            if (!subscribedTags.ContainsKey(tagName))
            {
                var subscribedTag = new SubscribedTag(tag);
                subscribedTags.TryAdd(tagName, subscribedTag);
            }
        }

        public bool IsTagSubscribed(string tagName)
        {
            return subscribedTags.ContainsKey(tagName);
        }

        public void UnsubscribeTag(string tagName)
        {
            subscribedTags.TryRemove(tagName, out _);
        }

        public (Tag?, bool) GetSubscribedTag(string tagName)
        {
            if (subscribedTags.TryGetValue(tagName, out var subscribedTag))
                return (subscribedTag.Tag, subscribedTag.IsStale);
            else
            {
                return (null, false);
            }
        }

        // sync / async busy status
        public Task WaitUntilIdleAsync() => idleTask.Task;
        public void WaitUntilIdle() => idleTask.Task.GetAwaiter().GetResult();

        // subscription read loop
        private async void ProcessSubscriptionReads(CancellationToken cancel)
        {
            try
            {
                while (await subscriptionTimer.WaitForNextTickAsync(cancel))
                {
                    // set all stale
                    foreach (var stale in subscribedTags.Values)
                        stale.SetStale();

                    if (!driver.IsConnected) return;

                    // prevent re-entry
                    if (Interlocked.Exchange(ref isSubscriptionProcessing, 1) == 1)
                        return;

                    // set busy
                    idleTask = new(TaskCreationOptions.RunContinuationsAsynchronously);

                    try
                    {
                        // read tags
                        foreach (var subscribed in subscribedTags.Values)
                        {
                            await subscribed.Tag.ReadAsync(cancel);
                            subscribed.ClearStale();
                        }

                    }
                    catch (Exception ex)
                    {
                        // timeout exception on disconnect
                        Console.WriteLine(ex.ToString());
                    }
                    finally
                    {
                        // allow re-entry
                        Interlocked.Exchange(ref isSubscriptionProcessing, 0);
                        idleTask.TrySetResult();
                    }
                }
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                // Expected shutdown
            }

        }

        public void Dispose()
        {
            subscriptionCts?.Cancel();
            subscriptionTimer.Dispose();
        }

        internal class SubscribedTag
        {
            public readonly Tag Tag;
            public bool IsStale => stale > 1;
            private volatile int stale = 2;
            public SubscribedTag(Tag tag)
            {
                Tag = tag;
            }
            internal void SetStale() => stale += 1;
            internal void ClearStale() => stale = 0;
        }
    }
}
