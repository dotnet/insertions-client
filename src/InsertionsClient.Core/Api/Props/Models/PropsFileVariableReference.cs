// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.DotNet.InsertionsClient.Props.Models
{
    /// <summary>
    /// Represents a reference to a variable defined in a props file
    /// </summary>
    public sealed class PropsFileVariableReference
    {
        /// <summary>
        /// Name of the defined variable
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Desired value of the variable
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// The file that was referencing this variable. Used when resolving the correct scope
        /// for the variable.
        /// </summary>
        public string ReferencedFilePath { get; private set; }

        /// <summary>
        /// Creates an instance of <see cref="PropsFileVariableReference"/>.
        /// </summary>
        /// <param name="name">Name of the variable</param>
        /// <param name="value">Desired value of the variable</param>
        /// <param name="referencedFilePath">The file that was referencing this variable.</param>
        internal PropsFileVariableReference(string name, string value, string referencedFilePath)
        {
            Name = name;
            Value = value;
            ReferencedFilePath = referencedFilePath;
        }
    }
}
