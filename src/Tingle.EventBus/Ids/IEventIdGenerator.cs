using Tingle.EventBus.Registrations;

namespace Tingle.EventBus.Ids
{
    /// <summary>
    /// Generator for event identifiers
    /// </summary>
    public interface IEventIdGenerator
    {
        /// <summary>
        /// Generate value for <see cref="EventContext.Id"/>.
        /// </summary>
        /// <param name="reg">The <see cref="EventRegistration"/> for which to generate for.</param>
        /// <returns></returns>
        string Generate(EventRegistration reg);
    }
}
