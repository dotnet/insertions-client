// Copyright (c) Microsoft. All rights reserved.

using System.Text.RegularExpressions;

namespace Microsoft.Net.Insertions.Api.Props.Models
{
    /// <summary>
    /// Represents a payload defined in the swr file.
    /// </summary>
    internal sealed class PayloadPath
    {
        /// <summary>
        /// Pattern that will match the path to an actual file on disk, exposing the value of the variable.
        /// Precompiled to allow efficient reuse.
        /// </summary>
        public Regex Pattern { get; private set; }

        /// <summary>
        /// Name of the variable referenced in the path.
        /// </summary>
        public string VariableName { get; private set; }

        /// <summary>
        /// Creates an instance of <see cref="PayloadPath"/>
        /// </summary>
        /// <param name="pattern">Pattern that will match the path to an actual file on disk, 
        ///	exposing the value of the variable.</param>
        /// <param name="variableName">Name of the variable, referenced in the path</param>
        internal PayloadPath(Regex pattern, string variableName)
        {
            Pattern = pattern;
            VariableName = variableName;
        }
    }
}