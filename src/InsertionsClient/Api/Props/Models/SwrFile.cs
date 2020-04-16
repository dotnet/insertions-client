// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.Net.Insertions.Api.Models
{
	/// <summary>
	/// Represents an SWR file in a VS repo local working directory.
	/// </summary>
	internal class SwrFile
	{
		/// <summary>
		/// Full path to the swr file.
		/// </summary>
		public readonly string Path;

		/// <summary>
		/// Paths to payloads mentioned in the file.
		/// </summary>
		public readonly List<PayloadPath> PayloadPaths = new List<PayloadPath>();

		/// <summary>
		/// Represents a payload defined in the swr file.
		/// </summary>
		public class PayloadPath
		{
			/// <summary>
			/// Pattern that will match the path to an actual file on disk, exposing the value of the variable.
			/// Precompiled to allow efficient reuse.
			/// </summary>
			public readonly Regex Pattern;

			/// <summary>
			/// Name of the variable referenced in the path.
			/// </summary>
			public readonly string VariableName;

			/// <summary>
			/// Creates an instance of <see cref="PayloadPath"/>
			/// </summary>
			/// <param name="pattern">Pattern that will match the path to an actual file on disk, 
			///	exposing the value of the variable.</param>
			/// <param name="variableName">Name of the variable, referenced in the path</param>
			public PayloadPath(Regex pattern, string variableName)
			{
				Pattern = pattern;
				VariableName = variableName;
			}
		}

		/// <summary>
		/// Creates an instance of <see cref="SwrFile"/>.
		/// Doesn't access the file, only stores the path.
		/// </summary>
		/// <param name="path">Path to the file on disk</param>
		public SwrFile(string path)
		{
			Path = path;
		}
	}
}
