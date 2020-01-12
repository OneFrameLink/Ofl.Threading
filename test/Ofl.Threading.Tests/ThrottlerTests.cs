using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ofl.Threading.Tests
{
    public class ThrottlerTests
    {
        [Theory]
        [InlineData(2, 2, null, "00:00:01", false)]
        [InlineData(3, 2, null, "00:00:01", true)]
        [InlineData(3, 2, "00:00:02", "00:00:01", false)]
        [InlineData(2600, 2500, null, "00:00:01", true)]
        public async Task Test_Throttle(
            int iterations, 
            int maxCount, 
            string? delayString, 
            string windowString, 
            bool expectedResult
        )
        {
            // The cancellation token.
            CancellationToken cancellationToken = CancellationToken.None;

            // Validate parameters.
            if (iterations <= 0) throw new ArgumentOutOfRangeException(nameof(iterations), iterations, $"The { nameof(iterations) } parameter must be a positive value.");

            // Parse the delay and the window.
            TimeSpan? delay = string.IsNullOrWhiteSpace(delayString) ? (TimeSpan?) null : TimeSpan.Parse(delayString);
            TimeSpan window = TimeSpan.Parse(windowString);

            // Create the throttle.
            var throttler = new Throttler(maxCount, window);

            // The value.
            bool result = false;

            // Cycle.
            foreach (int i in Enumerable.Range(0, iterations))
            {
                // Get the value.  Do not short circut.
                bool value = await throttler.ThrottleAsync(cancellationToken).ConfigureAwait(false);

                // Merge.
                result |= value;

                // Delay if necessary.
                if (delay != null) await Task.Delay(delay.Value, cancellationToken).ConfigureAwait(false);
            }

            // Compare.
            Assert.Equal(expectedResult, result);
        }
    }
}
