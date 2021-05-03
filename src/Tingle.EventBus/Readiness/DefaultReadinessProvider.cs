using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Readiness
{
    internal class DefaultReadinessProvider : IReadinessProvider
    {
        /// <inheritdoc/>
        public Task<bool> ConsumerReadyAsync(EventRegistration ereg, EventConsumerRegistration creg, CancellationToken cancellationToken = default)
        {
            // TODO: implement this
            return Task.FromResult(true);
        }
    }
}
