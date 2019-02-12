using System;

namespace Datadog.Trace.Interfaces
{
    /// <summary>
    ///     Used for generating Random objects for use
    /// </summary>
    internal interface IRandomProvider
    {
        /// <summary>
        ///     Generates a Random object for use
        /// </summary>
        /// <returns>A Random object for use</returns>
        Random GetRandom();
    }
}
