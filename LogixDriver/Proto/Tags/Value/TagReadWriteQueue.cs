using libplctag;
using System.Threading.Channels;

namespace Logix.Proto
{
    /// <summary>
    /// Producer/consumer queues for managing async tag read/write operations.
    /// Ensures read/write operations are handled on a single task/thread.
    /// Executes cyclically with a max number of operations per cycle.
    /// </summary>
    public class TagReadWriteQueue : ITagReadWriteQueue, IDisposable
    {
        private abstract record QueuedOperation(string TagName)
        {
            public TaskCompletionSource<Tag> CompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private sealed record ReadOperation(Tag Tag) : QueuedOperation(Tag.Name);
        private sealed record WriteOperation(Tag Tag) : QueuedOperation(Tag.Name);

        private readonly ChannelWriter<ReadOperation> readChannelWriter;
        private readonly ChannelReader<ReadOperation> readChannelReader;
        private readonly ChannelWriter<WriteOperation> writeChannelWriter;
        private readonly ChannelReader<WriteOperation> writeChannelReader;
        private readonly Driver driver;
        private Task? pollingTask;
        private CancellationTokenSource? pollingCts;

        // Track pending operations by tag name and type to prevent duplicates
        private readonly Dictionary<string, QueuedOperation> pendingOperations = new();

        public TagReadWriteQueue(Driver driver, uint pollingRateMs = 100)
        {
            this.driver = driver;

            // Separate unbounded channels for reads and writes
            var readChannel = Channel.CreateUnbounded<ReadOperation>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            var writeChannel = Channel.CreateUnbounded<WriteOperation>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            readChannelWriter = readChannel.Writer;
            readChannelReader = readChannel.Reader;
            writeChannelWriter = writeChannel.Writer;
            writeChannelReader = writeChannel.Reader;

            StartPolling(pollingRateMs);
        }

        /// <summary>
        /// Enqueue a read operation and return the value asynchronously.
        /// If a read for the same tag is already pending, returns the existing task.
        /// </summary>
        public Task<Tag> EnqueueReadAsync(Tag tag)
        {
            lock (pendingOperations)
            {
                var operationKey = $"READ:{tag.Name}";

                // If operation already pending, return existing task
                if (pendingOperations.TryGetValue(operationKey, out var existing) && existing is ReadOperation readOp)
                    return readOp.CompletionSource.Task;

                var operation = new ReadOperation(tag);
                if (!readChannelWriter.TryWrite(operation))
                    throw new InvalidOperationException("Failed to enqueue read operation. Queue may be closed.");

                pendingOperations[operationKey] = operation;
                return operation.CompletionSource.Task;
            }
        }

        /// <summary>
        /// Enqueue a read operation and wait synchronously for the result
        /// </summary>
        public Tag EnqueueReadSync(Tag tag)
        {
            var task = EnqueueReadAsync(tag);
            return task.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Enqueue a write operation and return completion asynchronously.
        /// Newer writes to the same tag replace older pending writes.
        /// </summary>
        public Task<Tag> EnqueueWriteAsync(Tag tag)
        {
            lock (pendingOperations)
            {
                var operationKey = $"WRITE:{tag.Name}";

                var operation = new WriteOperation(tag);

                // If a write already exists for this tag, cancel it and replace with new one
                if (pendingOperations.TryGetValue(operationKey, out var existing) && existing is WriteOperation)
                {
                    existing.CompletionSource.TrySetCanceled();
                }

                if (!writeChannelWriter.TryWrite(operation))
                    throw new InvalidOperationException("Failed to enqueue write operation. Queue may be closed.");

                pendingOperations[operationKey] = operation;
                return operation.CompletionSource.Task;
            }
        }

        /// <summary>
        /// Enqueue a write operation and wait synchronously for completion
        /// </summary>
        public Tag EnqueueWriteSync(Tag tag)
        {
            var task = EnqueueWriteAsync(tag);
            return task.GetAwaiter().GetResult();
        }

        private void StartPolling(uint pollingRateMs)
        {
            pollingCts = new CancellationTokenSource();
            pollingTask = Task.Run(() => ProcessQueueLoop(pollingRateMs, pollingCts.Token), pollingCts.Token);
        }

        private async Task ProcessQueueLoop(uint pollingRateMs, CancellationToken cancel)
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(pollingRateMs));

                while (await timer.WaitForNextTickAsync(cancel))
                {
                    if (!driver.IsConnected)
                        continue;

                    int processed = 0;
                    const int maxPerCycle = 100;

                    // Process all writes first (higher priority)
                    while (processed < maxPerCycle && writeChannelReader.TryRead(out var writeOperation))
                    {
                        await ProcessOperation(writeOperation, cancel);
                        processed++;
                    }

                    // Then process reads with remaining capacity
                    while (processed < maxPerCycle && readChannelReader.TryRead(out var readOperation))
                    {
                        await ProcessOperation(readOperation, cancel);
                        processed++;
                    }
                }
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                // Expected shutdown
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error in queue processing loop: {ex}");
            }
        }

        private async Task ProcessOperation(QueuedOperation operation, CancellationToken cancel)
        {
            try
            {
                switch (operation)
                {
                    case ReadOperation readOp:
                        await readOp.Tag.ReadAsync(cancel);
                        //var value = driver.ValueResolver.ResolveValue(readOp.Tag, readOp.Definition);
                        readOp.CompletionSource.TrySetResult(readOp.Tag);
                        break;

                    case WriteOperation writeOp:
                        if (!writeOp.Tag.IsInitialized)
                            writeOp.Tag.Initialize();
                        //driver.ValueResolver.WriteTagBuffer(writeOp.Tag, writeOp.Definition, writeOp.Value);
                        await writeOp.Tag.WriteAsync(cancel);
                        writeOp.CompletionSource.TrySetResult(writeOp.Tag);
                        break;
                }
            }
            catch (Exception ex)
            {
                operation.CompletionSource.TrySetException(ex);
            }
            finally
            {
                // Remove from pending tracking when complete
                lock (pendingOperations)
                {
                    var operationKey = operation is ReadOperation
                        ? $"READ:{operation.TagName}"
                        : $"WRITE:{operation.TagName}";
                    pendingOperations.Remove(operationKey);
                }
            }
        }

        public void Dispose()
        {
            writeChannelWriter.TryComplete();
            readChannelWriter.TryComplete();
            pollingCts?.Cancel();

            try
            {
                pollingTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
                // Expected if already cancelled
            }

            pollingCts?.Dispose();
        }
    }
}