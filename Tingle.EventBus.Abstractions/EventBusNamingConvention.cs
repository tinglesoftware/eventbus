namespace Tingle.EventBus.Abstractions
{
    public enum EventBusNamingConvention
    {
        /// <summary>
        /// The type name is unchanged.
        /// </summary>
        Unchanged,

        /// <summary>
        /// The type name is converted to <see href="https://en.wiktionary.org/wiki/kebab_case">Kebab case</see>
        /// </summary>
        KebabCase,

        /// <summary>
        /// The type name is converted to <see href="https://en.wiktionary.org/wiki/snake_case">Snake case</see>.
        /// </summary>
        SnakeCase,
    }
}
