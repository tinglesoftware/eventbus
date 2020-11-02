using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using Tingle.EventBus.Abstractions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="EventBusOptions"/>.
    /// </summary>
    internal class EventBusPostConfigureOptions : IPostConfigureOptions<EventBusOptions>
    {
        public void PostConfigure(string name, EventBusOptions options)
        {
            
        }
    }
}
