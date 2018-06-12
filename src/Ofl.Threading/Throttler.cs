using System;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using AsyncLock = Nito.AsyncEx.AsyncLock;

namespace Ofl.Threading
{
    public class Throttler
    {
        #region Constructor

        public Throttler(int maxCount, TimeSpan window) : this(maxCount, window, DefaultScheduler.Instance)
        { }

        public Throttler(int maxCount, TimeSpan window, IScheduler scheduler)
        {
            // Validate parameters.
            if (maxCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, $"The { nameof(maxCount) } parameter must be a positive value.");
            if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window), maxCount, $"The { nameof(window) } parameter must be a positive value.");
            Scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));

            // Assign values.
            MaxCount = maxCount;
            Window = window;
        }

        #endregion

        #region Instance state.

        public int MaxCount { get; }

        public TimeSpan Window { get; }

        public IScheduler Scheduler { get; }

        private int _count;

        private DateTimeOffset? _windowStart;

        private DateTimeOffset _windowEnd = DateTimeOffset.MaxValue;

        private static readonly Task<bool> TrueTask = Task.FromResult(true);

        private static readonly Task<bool> FalseTask = Task.FromResult(false);

        private readonly AsyncLock _lock = new AsyncLock();

        #endregion

        #region Throttle and helpers.

        private Task<bool> ResetAsync(DateTimeOffset windowStart, bool result, CancellationToken cancellationToken)
        {
            // Set the values.
            _windowStart = windowStart;
            _windowEnd = windowStart + Window;
            _count = 1;

            // Return the task.
            return result ? TrueTask : FalseTask;
        }

        public async Task<bool> ThrottleAsync(CancellationToken cancellationToken)
        {
            // Dispose when done.
            using (await _lock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                // Get the current time.
                DateTimeOffset now = Scheduler.Now;

                // If:
                //      - Window start is null
                //      - Now is greater than window end
                // Then reset and return.
                if (_windowStart == null || now > _windowEnd)
                    // Reset with now.
                    return await ResetAsync(now, false, cancellationToken);

                // Increment the count.
                _count++;

                // If the count is not greater than the max count, then get out.
                if (_count <= MaxCount)
                    return await FalseTask.ConfigureAwait(false);

                // Create the task completion source.
                var tcs = new TaskCompletionSource<bool>();

                // Schedule.
                Scheduler.Schedule((object) null, _windowEnd, (s, o) => {
                    // Reset.
                    ResetAsync(s.Now, true, cancellationToken);

                    // Set the result.
                    tcs.TrySetResult(true);

                    // Return null.
                    return null;
                });

                // Wait on the task.
                return await tcs.Task.ConfigureAwait(false);
            }
        }

        #endregion
    }
}
