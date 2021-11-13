using System;
using System.Diagnostics;
using System.Reflection;

namespace Tingle.EventBus.Diagnostics
{
    ///
    public static class EventBusActivitySource
    {
        private static readonly AssemblyName AssemblyName = typeof(EventBusActivitySource).Assembly.GetName();
        private static readonly string ActivitySourceName = AssemblyName.Name!;
        private static readonly Version Version = AssemblyName.Version!;
        private static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version.ToString());

        /// <summary>
        /// Creates a new activity if there are active listeners for it, using the specified
        /// name, activity kind, and parent Id.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="kind"></param>
        /// <param name="parentId"></param>
        /// <returns></returns>
        public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal, string? parentId = null)
        {
            return parentId is not null
                ? ActivitySource.StartActivity(name: name, kind: kind, parentId: parentId)
                : ActivitySource.StartActivity(name: name, kind: kind);
        }
    }
}
