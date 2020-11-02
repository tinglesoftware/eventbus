namespace Tingle.EventBus.Serialization
{
    public class HostInfo
    {
        /// <summary>
        /// The machine name (or role instance name) of the machine.
        /// </summary>
        public string MachineName { get; internal set; }

        /// <summary>
        /// The name of the application.
        /// </summary>
        public string ApplicationName { get; internal set; }

        /// <summary>
        /// The version of the application.
        /// </summary>
        public string ApplicationVersion { get; internal set; }

        /// <summary>
        /// The name of the environment the application is running in.
        /// </summary>
        public string EnvironmentName { get; internal set; }

        /// <summary>
        /// The version of the library.
        /// </summary>
        public string LibraryVersion { get; internal set; }

        /// <summary>
        /// The operating system hosting the application.
        /// </summary>
        public string OperatingSystem { get; internal set; }
    }
}
