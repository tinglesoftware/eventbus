using System.IO;

namespace System
{
    /// <summary>
    /// Extension methods for <see cref="BinaryData"/>
    /// </summary>
    internal static class BinaryDataExtensions
    {
        /// <summary>
        /// Converts the <see cref="BinaryData"/> to a <see cref="MemoryStream"/>.
        /// </summary>
        /// <param name="data">The <see cref="BinaryData"/> to be converted.</param>
        /// <returns>A <see cref="MemoryStream"/> representing the data.</returns>
        public static MemoryStream ToMemoryStream(this BinaryData data) => new MemoryStream(data.ToArray());
    }
}
