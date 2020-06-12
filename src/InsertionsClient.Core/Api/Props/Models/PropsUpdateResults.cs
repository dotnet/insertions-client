// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Models;
using System.Collections.Generic;

namespace Microsoft.DotNet.InsertionsClient.Props.Models
{
    /// <summary>
    /// Represents the result of a PropsFile update operation
    /// </summary>
    public sealed class PropsUpdateResults
    {
        internal readonly List<PropsFileVariableReference> _unrecognizedVariables = new List<PropsFileVariableReference>();

        internal readonly Dictionary<PropsFile, List<PropsFileVariableReference>> _updatedVariables = new Dictionary<PropsFile, List<PropsFileVariableReference>>();

        internal readonly List<FileSaveResult> _modifiedFiles = new List<FileSaveResult>();

        /// <summary>
        /// Overall result of the update operation.
        /// True for success, false for failure.
        /// </summary>
        public bool Outcome { get; set; }

        /// <summary>
        /// Text, explaining the reasoning behind the outcome.
        /// </summary>
        public string? OutcomeDetails { get; set; }

        /// <summary>
        /// Variables that were requested to be changed, but wasn't found in any of the props files.
        /// </summary>
        public IReadOnlyList<PropsFileVariableReference> UnrecognizedVariables => _unrecognizedVariables;

        /// <summary>
        /// Variables that were successfully found and modified, keyed by the <see cref="PropsFile"/> that stores them.
        /// </summary>
        public IReadOnlyDictionary<PropsFile, List<PropsFileVariableReference>> UpdatedVariables => _updatedVariables;

        /// <summary>
        /// Result of each operation that attempted to save a <see cref="PropsFile"/> to disk.
        /// </summary>
        public IReadOnlyList<FileSaveResult> ModifiedFiles => _modifiedFiles;
    }
}
