// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Models;
using System.Collections.Generic;

namespace Microsoft.Net.Insertions.Props.Models
{
    /// <summary>
    /// Represents the result of a PropsFile update operation
    /// </summary>
    public class PropsUpdateResults
    {
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
        public List<PropsFileVariableReference> UnrecognizedVariables { get; private set; } = new List<PropsFileVariableReference>();

        /// <summary>
        /// Variables that were successfully found and modified, keyed by the <see cref="PropsFile"/> that stores them.
        /// </summary>
        public Dictionary<PropsFile, List<PropsFileVariableReference>> UpdatedVariables { get; private set; } = new Dictionary<PropsFile, List<PropsFileVariableReference>>();

        /// <summary>
        /// Result of each operation that attempted to save a <see cref="PropsFile"/> to disk.
        /// </summary>
        public List<FileSaveResult> ModifiedFiles { get; set; } = new List<FileSaveResult>();
    }
}
