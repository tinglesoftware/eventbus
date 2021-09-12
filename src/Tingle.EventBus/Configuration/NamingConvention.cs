﻿namespace Tingle.EventBus.Configuration
{
    /// <summary>
    /// The naming convention used when generating names from types.
    /// </summary>
    public enum NamingConvention
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

        /// <summary>
        /// The type name is transformed into a lower case string with a period between words.
        /// </summary>
        DotCase,
    }
}
