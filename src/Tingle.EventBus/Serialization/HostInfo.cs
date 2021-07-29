namespace Tingle.EventBus.Serialization
{
    /// <summary>
    /// Information about the host on which the event bus is running.
    /// </summary>
    public class HostInfo
    {
        /// <summary>
        /// The machine name (or role instance name) of the machine.
        /// </summary>
        /// <example>WIN-HQ1243</example>
        public virtual string? MachineName { get; set; }

        /// <summary>
        /// The name of the application.
        /// </summary>
        /// <example>Tingle.EventBus.Examples.SimplePublisher</example>
        public virtual string? ApplicationName { get; set; }

        /// <summary>
        /// The version of the application.
        /// </summary>
        /// <example>1.0.0.0</example>
        public virtual string? ApplicationVersion { get; set; }

        /// <summary>
        /// The name of the environment the application is running in.
        /// </summary>
        /// <example>Production</example>
        public virtual string? EnvironmentName { get; set; }

        /// <summary>
        /// The version of the library.
        /// </summary>
        /// <example>1.0.0.0</example>
        public virtual string? LibraryVersion { get; set; }

        /// <summary>
        /// The operating system hosting the application.
        /// </summary>
        /// <example>Microsoft Windows NT 10.0.19042.0</example>
        public virtual string? OperatingSystem { get; set; }
    }
}
