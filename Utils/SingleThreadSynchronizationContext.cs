using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OtlpServer.Utils
{
    public sealed class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<KeyValuePair<SendOrPostCallback, object>> queue = new();

        private int contextThreadId = -1;
        private bool disposed = false;

        public SingleThreadSynchronizationContext() { }

        public TaskScheduler TaskScheduler => new SyncContextTaskScheduler(this);

        public override void Post(SendOrPostCallback d, object state)
        {
            if (d == null)
            {
                throw new ArgumentNullException(nameof(d));
            }

            ThrowIfDisposed();
            queue.Add(new KeyValuePair<SendOrPostCallback, object>(d, state));
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            if (d == null)
            {
                throw new ArgumentNullException(nameof(d));
            }

            ThrowIfDisposed();

            if (Environment.CurrentManagedThreadId == contextThreadId)
            {
                d(state);
                return;
            }

            Exception dispatchException = null;
            using (var done = new ManualResetEventSlim(false))
            {
                SendOrPostCallback wrapper = s =>
                {
                    try
                    {
                        d(s);
                    }
                    catch (Exception ex)
                    {
                        dispatchException = ex;
                    }
                    finally
                    {
                        done.Set();
                    }
                };

                queue.Add(new KeyValuePair<SendOrPostCallback, object>(wrapper, state));
                done.Wait();
            }

            if (dispatchException != null)
            {
                throw new AggregateException("Exception was thrown inside synchronization context.", dispatchException);
            }

        }

        public void RunOnCurrentThread()
        {
            ThrowIfDisposed();

            contextThreadId = Environment.CurrentManagedThreadId;

            try
            {
                foreach (var item in queue.GetConsumingEnumerable())
                {
                    try
                    {
                        item.Key(item.Value);
                    }
                    catch
                    {
                        throw;
                    }
                }
            }
            finally
            {
                contextThreadId = -1;
            }
        }

        public void Complete()
        {
            if (!queue.IsAddingCompleted)
            {
                queue.CompleteAdding();
            }

        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SingleThreadSynchronizationContext));
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                Complete();
                queue.Dispose();
            }
        }

        private sealed class SyncContextTaskScheduler : TaskScheduler
        {
            private readonly SingleThreadSynchronizationContext context;

            public SyncContextTaskScheduler(SingleThreadSynchronizationContext context)
            {
                this.context = context ?? throw new ArgumentNullException(nameof(context));
            }

            protected override void QueueTask(Task task)
            {
                context.Post(state => TryExecuteTask((Task)state), task);
            }

            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            {
                if (Thread.CurrentThread.ManagedThreadId == context.contextThreadId)
                {
                    return TryExecuteTask(task);
                }

                return false;
            }

            protected override IEnumerable<Task> GetScheduledTasks()
            {
                return null;
            }
        }
    }
}