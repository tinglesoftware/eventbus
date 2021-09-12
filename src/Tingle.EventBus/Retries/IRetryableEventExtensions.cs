using Polly.Contrib.WaitAndRetry;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Tingle.EventBus.Retries
{
    /// <summary>Extension methods for <see cref="IRetryableEvent"/>.</summary>
    public static class IRetryableEventExtensions
    {
        #region Delays

        /// <summary>
        /// Generates and sets delay durations in an exponentially backing-off, jittered manner, making
        /// sure to mitigate any correlations.
        /// For example: 850ms, 1455ms, 3060ms. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <param name="medianFirstRetryDelay">
        /// The median delay to target before the first retry, call it f (= f * 2^0).
        /// Choose this value both to approximate the first delay, and to scale the remainder of the series.
        /// Subsequent retries will (over a large sample size) have a median approximating retries
        /// at time f * 2^1, f * 2^2 ... f * 2^t etc for try t.
        /// The actual amount of delay-before-retry for try t may be distributed between 0 and
        /// f * (2^(t+1) - 2^(t-1)) for t >= 2; or between 0 and f * 2^(t+1), for t is 0 or 1.
        /// </param>
        /// <param name="retries">The maximum number of retries to use, in addition to the original call.</param>
        /// <param name="seed">
        /// An optional System.Random seed to use. If not specified, will use a shared instance
        /// with a random seed, per Microsoft recommendation for maximum randomness.
        /// </param>
        /// <param name="fastFirst">Whether the first retry will be immediate or not.</param>
        /// <returns></returns>
        public static T WithJitterDelays<T>(this T @event, TimeSpan medianFirstRetryDelay, int retries, int? seed = null, bool fastFirst = false)
            where T : IRetryableEvent
        {
            var delays = Backoff.DecorrelatedJitterBackoffV2(medianFirstRetryDelay: medianFirstRetryDelay,
                                                             retryCount: retries,
                                                             seed: seed,
                                                             fastFirst: fastFirst);
            return @event.WithDelays(delays);
        }

        /// <summary>
        /// Generates and sets delay durations in an exponential manner.
        /// The formula used is: Duration = initialDelay x 2^iteration.
        /// For example: 100ms, 200ms, 400ms, 800ms, ...
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <param name="initialDelay">The duration value for the wait before the first retry.</param>
        /// <param name="retries">The maximum number of retries to use, in addition to the original call.</param>
        /// <param name="factor">The exponent to multiply each subsequent duration by.</param>
        /// <param name="fastFirst">Whether the first retry will be immediate or not.</param>
        /// <returns></returns>
        public static T WithExponentialDelays<T>(this T @event, TimeSpan initialDelay, int retries, double factor = 2, bool fastFirst = false)
            where T : IRetryableEvent
        {
            var delays = Backoff.ExponentialBackoff(initialDelay: initialDelay, retryCount: retries, factor: factor, fastFirst: fastFirst);
            return @event.WithDelays(delays);
        }

        /// <summary>
        /// Generates and sets delay durations in an linear manner.
        /// The formula used is: Duration = initialDelay x (1 + factor x iteration).
        /// For example: 100ms, 200ms, 300ms, 400ms, ...
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <param name="initialDelay">The duration value for the first retry.</param>
        /// <param name="retries">The maximum number of retries to use, in addition to the original call.</param>
        /// <param name="factor">The linear factor to use for increasing the duration on subsequent calls.</param>
        /// <param name="fastFirst">Whether the first retry will be immediate or not.</param>
        /// <returns></returns>
        public static T WithLinearDelays<T>(this T @event, TimeSpan initialDelay, int retries, double factor = 2, bool fastFirst = false)
            where T : IRetryableEvent
        {
            var delays = Backoff.LinearBackoff(initialDelay: initialDelay, retryCount: retries, factor: factor, fastFirst: fastFirst);
            return @event.WithDelays(delays);
        }

        /// <summary>
        /// Generates and sets delay durations as a constant value.
        /// The formula used is: Duration = delay.
        /// For example: 200ms, 200ms, 200ms, ...
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <param name="delay">The constant wait duration before each retry.</param>
        /// <param name="retries">The maximum number of retries to use, in addition to the original call.</param>
        /// <param name="fastFirst">Whether the first retry will be immediate or not.</param>
        /// <returns></returns>
        public static T WithConstantDelays<T>(this T @event, TimeSpan delay, int retries, bool fastFirst = false)
            where T : IRetryableEvent
        {
            var delays = Backoff.ConstantBackoff(delay: delay, retryCount: retries, fastFirst: fastFirst);
            return @event.WithDelays(delays);
        }

        private static T WithDelays<T>(this T @event, IEnumerable<TimeSpan> delays) where T : IRetryableEvent
        {
            @event.Delays = delays.ToList();
            return @event;
        }

        #endregion

        #region Attempts

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <param name="attempt"></param>
        /// <returns></returns>
        public static T AddAttempt<T>(this T @event, DateTimeOffset attempt) where T : IRetryableEvent
        {
            var attempts = @event.Attempts;
            attempts.Add(attempt);

            return @event;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        /// <returns></returns>
        public static T AddAttempt<T>(this T @event) where T : IRetryableEvent
        {
            return @event.AddAttempt(DateTimeOffset.UtcNow);
        }

        /// <summary>Whether this event is doing it's last attempt.</summary>
        public static bool IsLastAttempt(this IRetryableEvent @event)
        {
            if (@event is null) throw new ArgumentNullException(nameof(@event));
            if (@event.Delays is null) throw new ArgumentNullException(nameof(@event.Delays));
            if (@event.Attempts is null) throw new ArgumentNullException(nameof(@event.Attempts));

            return (@event.Delays.Count - @event.Attempts.Count) == 1;
        }

        /// <summary>Trys to get the delay for the next retry if any.</summary>
        /// <param name="event"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        public static bool TryGetNextRetryDelay(this IRetryableEvent @event, [NotNullWhen(true)] out TimeSpan? delay)
        {
            var attempts = @event.Attempts;
            var delays = @event.Delays;

            if (attempts.Count >= (delays.Count + 1))
            {
                delay = null;
                return false;
            }

            delay = @event.Delays.Skip(attempts.Count - 1).First();
            return true;
        }

        #endregion
    }
}
