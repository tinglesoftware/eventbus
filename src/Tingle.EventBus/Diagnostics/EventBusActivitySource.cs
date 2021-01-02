using System;
using System.Diagnostics;
using System.Reflection;

namespace Tingle.EventBus.Diagnostics
{
    internal class EventBusActivitySource
    {
        internal static readonly AssemblyName AssemblyName = typeof(EventBusActivitySource).Assembly.GetName();
        internal static readonly string ActivitySourceName = AssemblyName.Name;
        internal static readonly Version Version = AssemblyName.Version;
        internal static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName, Version.ToString());
    }
}
