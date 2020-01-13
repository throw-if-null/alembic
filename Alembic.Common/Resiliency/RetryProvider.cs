using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System;
using System.Threading.Tasks;

namespace Alembic.Common.Resiliency
{
    public interface IRetryProvider
    {
        Task RetryOn<TException, TResult>(Func<TException, bool> exceptionPredicate, Func<TResult, bool> resultPredicate, Func<Task<TResult>> execute) where TException : Exception;
    }

    public class RetryProvider : IRetryProvider
    {
        private readonly RetryProviderOptions _options;
        private readonly ILogger _logger;

        public RetryProvider(IOptions<RetryProviderOptions> options, ILogger<RetryProvider> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task RetryOn<TException, TResult>(
            Func<TException, bool> exceptionPredicate,
            Func<TResult, bool> resultPredicate,
            Func<Task<TResult>> execute)
            where TException : Exception
        {
            return
                Policy
                    .Handle(exceptionPredicate)
                    .OrResult(resultPredicate)
                    .WaitAndRetryAsync(
                        _options.Delays.Count,
                        i =>
                        {
                            _logger.LogDebug("Retry attempt: {0}", i);

                            return TimeSpan.FromMilliseconds(_options.Delays[i - 1] + GetJitter());
                        })
                    .ExecuteAsync(execute);
        }

        private static readonly Func<double> GetJitter = delegate ()
        {
            var jitter = TimeSpan.FromMilliseconds(new Random().Next(0, 100)).TotalMilliseconds;

            return jitter;
        };
    }
}