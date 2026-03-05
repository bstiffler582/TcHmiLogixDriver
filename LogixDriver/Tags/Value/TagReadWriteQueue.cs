using Logix.Driver;
using libplctag;
using System.Threading.Channels;

namespace Logix.Tags
{
    /// <summary>
    /// Producer/consumer queues for managing async tag read/write operations.
    /// Ensures read/write operations are handled on a single task/thread.
    /// Executes cyclically with a max number of operations per cycle.
    /// </summary>
    public class TagReadWriteQueue : ITagReadWriteQueue
    {
        private abstract record QueuedOperation(string TagName)
        {
            public TaskCompletionSource<Tag> CompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private sealed record ReadOperation(Tag Tag) : QueuedOperation(Tag.Name);
        private sealed record WriteOperation(Tag Tag) : QueuedOperation(Tag.Name);
        private sealed record InitializeOperation(Tag Tag) : QueuedOperation(Tag.Name);

        private readonly ChannelWriter<ReadOperation> readChannelWriter;
        private readonly ChannelReader<ReadOperation> readChannelReader;
        private readonly ChannelWriter<InitializeOperation> initChannelWriter;
        private readonly ChannelReader<InitializeOperation> initChannelReader;
        private readonly ChannelWriter<WriteOperation> writeChannelWriter;
        private readonly ChannelReader<WriteOperation> writeChannelReader;
        
        private Task? pollingTask;
        private CancellationTokenSource? pollingCts;

        // Track pending operations by tag name and type to prevent duplicates
        private readonly Dictionary<string, QueuedOperation> pendingOperations = new();

        public TagReadWriteQueue(uint pollingRateMs = 100)
        {
            // Separate unbounded channels for reads and writes
            var readChannel = Channel.CreateUnbounded<ReadOperation>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            var initChannel = Channel.CreateUnbounded<InitializeOperation>(new UnboundedChannelOptions
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
            initChannelWriter = initChannel.Writer;
            initChannelReader = initChannel.Reader;
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

        public Task<Tag> EnqueueInitializeAsync(Tag tag)
        {
            lock (pendingOperations)
            {
                var operationKey = $"INIT:{tag.Name}";

                var operation = new InitializeOperation(tag);

                // If a write already exists for this tag, cancel it and replace with new one
                if (pendingOperations.TryGetValue(operationKey, out var existing) && existing is InitializeOperation)
                {
                    existing.CompletionSource.TrySetCanceled();
                }

                if (!initChannelWriter.TryWrite(operation))
                    throw new InvalidOperationException("Failed to enqueue initialize operation. Queue may be closed.");

                pendingOperations[operationKey] = operation;
                return operation.CompletionSource.Task;
            }
        }

        public Tag EnqueueInitializeSync(Tag tag)
        {
            var task = EnqueueInitializeAsync(tag);
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
                    int processed = 0;
                    const int maxPerCycle = 100;

                    // Process all inits first (highest priority)
                    while (processed < maxPerCycle && initChannelReader.TryRead(out var initOperation))
                    {
                        await ProcessOperation(initOperation, cancel);
                        processed++;
                    }

                    // Process all writes next (higher priority)
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
            string operationKey = string.Empty;

            try
            {
                switch (operation)
                {
                    case ReadOperation readOp:
                        await readOp.Tag.ReadAsync(cancel);
                        readOp.CompletionSource.TrySetResult(readOp.Tag);
                        operationKey = $"READ:{operation.TagName}";
                        break;

                    case InitializeOperation initOp:
                        if (!initOp.Tag.IsInitialized)
                            await initOp.Tag.InitializeAsync(cancel);
                        initOp.CompletionSource.TrySetResult(initOp.Tag);
                        operationKey = $"INIT:{operation.TagName}";
                        break;

                    case WriteOperation writeOp:
                        await writeOp.Tag.WriteAsync(cancel);
                        writeOp.CompletionSource.TrySetResult(writeOp.Tag);
                        operationKey = $"WRITE:{operation.TagName}";
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
                    pendingOperations.Remove(operationKey);
                }
            }
        }

        public void Dispose()
        {
            initChannelWriter.TryComplete();
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