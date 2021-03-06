﻿using Microsoft.Extensions.DependencyInjection;
using Polly.Retry;
using System;
using System.Linq;
using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Transports
{
    /// <summary>
    /// The base options for an event bus transport
    /// </summary>
    public abstract class EventBusTransportOptionsBase
    {
        /// <summary>
        /// The delay to introduce everytime zero messages are received.
        /// This eases on the CPU consumption and reduces the query costs.
        /// The default value is 1 minute. Max value is 10 minutes and minimum is 30 seconds.
        /// </summary>
        public TimeSpan EmptyResultsDelay { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets value indicating if the transport's entities
        /// (e.g. queues, topics, subscriptions, exchanges) should be created.
        /// If <see langword="false"/>, it is the responsibility of the developer to create
        /// entities.
        /// Always set this value to <see langword="false"/> when the credentials
        /// or connection string in use lack the requisite permissions for creation.
        /// Defaults to <see langword="true"/>.
        /// </summary>
        public bool EnableEntityCreation { get; set; } = true;

        /// <summary>
        /// The suffix to append to deadletter entities.
        /// Dead letter entities are created on transports where they are not created by default,
        /// and therefore look like normal entities.
        /// Defaults to <c>-dedletter</c>.
        /// </summary>
        public string DeadLetterSuffix { get; set; } = "-deadleter";

        /// <summary>
        /// The default value to use for <see cref="EntityKind"/> for events where it is not specified.
        /// To specify a value per event, use the <see cref="EventRegistration.EntityKind"/> option.
        /// </summary>
        public virtual EntityKind DefaultEntityKind { get; set; } = EntityKind.Queue;

        /// <summary>
        /// Optional default format to use for generated event identifiers when for events where it is not specified.
        /// This value overrides the default value set on the bus via <see cref="EventBusOptions.DefaultEventIdFormat"/>.
        /// To specify a value per consumer, use the <see cref="EventRegistration.IdFormat"/> option.
        /// </summary>
        public EventIdFormat? DefaultEventIdFormat { get; set; }

        /// <summary>
        /// Optional default retry policy to use for consumers where it is not specified.
        /// This value overrides the default value set on the bus via <see cref="EventBusOptions.DefaultConsumerRetryPolicy"/>.
        /// To specify a value per consumer, use the <see cref="EventConsumerRegistration.RetryPolicy"/> option.
        /// </summary>
        public AsyncRetryPolicy DefaultConsumerRetryPolicy { get; set; }

        /// <summary>
        /// Optional default behaviour for errors encountered in a consumer but are not handled.
        /// This value overrides the default value set on the bus via <see cref="EventBusOptions.DefaultUnhandledConsumerErrorBehaviour"/>.
        /// To specify a value per consumer, use the <see cref="EventConsumerRegistration.UnhandledErrorBehaviour"/> option.
        /// When an <see cref="AsyncRetryPolicy"/> is in force, only errors that are not handled by it will be subject to the value set here.
        /// Defaults to <see langword="null"/>.
        /// </summary>
        public UnhandledConsumerErrorBehaviour? DefaultUnhandledConsumerErrorBehaviour { get; set; }

        /// <summary>
        /// Ensures the value set for <see cref="EventRegistration.EntityKind"/> is among the allowed values.
        /// If no value is set, the default value (set via <see cref="DefaultEntityKind"/>) is set before checking.
        /// </summary>
        /// <param name="ereg">The <see cref="EventRegistration"/> to check.</param>
        /// <param name="allowed">The allowed values for <see cref="EntityKind"/>.</param>
        public void EnsureAllowedEntityKind(EventRegistration ereg, params EntityKind[] allowed)
        {
            if (ereg is null) throw new ArgumentNullException(nameof(ereg));
            if (allowed is null) throw new ArgumentNullException(nameof(allowed));

            // ensure there is a value (use default if none)
            ereg.EntityKind ??= DefaultEntityKind;

            // ensure the value is allowed
            var ek = ereg.EntityKind.Value;
            if (!allowed.Contains(ek))
            {
                throw new InvalidOperationException($"'{nameof(EntityKind)}.{ek}' is not permitted for '{ereg.TransportName}' transport.");
            }
        }

        /// <summary>
        /// Set value for <see cref="EventRegistration.IdFormat"/> with prioritization of the transport default over the bus default.
        /// </summary>
        /// <param name="ereg"></param>
        /// <param name="busOptions"></param>
        public void SetEventIdFormat(EventRegistration ereg, EventBusOptions busOptions)
        {
            // prioritize the transport
            ereg.IdFormat ??= DefaultEventIdFormat;
            ereg.IdFormat ??= busOptions.DefaultEventIdFormat;
        }
    }
}
