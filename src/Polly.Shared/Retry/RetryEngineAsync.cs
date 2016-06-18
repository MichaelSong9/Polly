﻿#if SUPPORTS_ASYNC

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Polly.Retry
{
    internal static partial class RetryEngine
    {
        internal static async Task<TResult> ImplementationAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action, 
            CancellationToken cancellationToken, 
            IEnumerable<ExceptionPredicate> shouldRetryExceptionPredicates,
            IEnumerable<ResultPredicate<TResult>> shouldRetryResultPredicates,
            Func<IRetryPolicyState> policyStateFactory, 
            bool continueOnCapturedContext)
        {
            IRetryPolicyState policyState = policyStateFactory();

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    TResult result = await action(cancellationToken).ConfigureAwait(continueOnCapturedContext);

                    if (!shouldRetryResultPredicates.Any(predicate => predicate(result)))
                    {
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        if (ex is OperationCanceledException && ((OperationCanceledException)ex).CancellationToken == cancellationToken)
                        {
                            throw;
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    if (!shouldRetryExceptionPredicates.Any(predicate => predicate(ex)))
                    {
                        throw;
                    }

                    if (!(await policyState
                        .CanRetryAsync(ex, cancellationToken, continueOnCapturedContext)
                        .ConfigureAwait(continueOnCapturedContext)))
                    {
                        throw;
                    }
                }
            }
        }
    }
}

#endif
