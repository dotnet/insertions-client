// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Api.Props.Models;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.DotNet.InsertionsClient.Api
{
    /// <summary>
    /// Finds and loads .swr files in a given directory.
    /// </summary>
    internal sealed class SwrFileReader
    {
        private readonly int _maxConcurrency;

        /// <summary>
        /// Pattern that finds payload path in swr file content.
        /// </summary>
        private Regex _payloadPattern = new Regex(@"\bvs.payload.*source=(.+?)($|\s)");

        /// <summary>
        /// Pattern that finds variable name in payload path
        /// </summary>
        private Regex _variablePattern = new Regex(@".*\$\((.*)\).*");

        /// <summary>
        /// Creates an instance of <see cref="SwrFileReader"/>
        /// </summary>
        /// <param name="maxConcurrency">Level of concurrency</param>
        public SwrFileReader(int maxConcurrency)
        {
            _maxConcurrency = maxConcurrency;
        }

        /// <summary>
        /// Loads all swr files under the given root.
        /// </summary>
        /// <param name="rootSearchDirectory">Directory that will be searched for swr files.
        /// Contained folders will automatically be included in the search.
        /// Path shouldn't be null, empty or whitespace.</param>
        /// <returns>A list of <see cref="SwrFile"/>s found under the root.</returns>
        public SwrFile[] LoadSwrFiles(string rootSearchDirectory)
        {
            DirectoryInfo rootDirectory = new DirectoryInfo(rootSearchDirectory);
            if (!rootDirectory.Exists)
            {
                return new SwrFile[0];
            }

            FileInfo[] files = rootDirectory.GetFiles("*.swr", SearchOption.AllDirectories);
            SwrFile[] swrFiles = new SwrFile[files.Length];

            ParallelOptions options = new ParallelOptions()
            {
                MaxDegreeOfParallelism = _maxConcurrency
            };

            _ = Parallel.For(0, files.Length, options, i => swrFiles[i] = LoadSwrFile(files[i]));

            return swrFiles;
        }

        private SwrFile LoadSwrFile(FileInfo file)
        {
            SwrFile swr = new SwrFile(file.FullName);

            string fileContent = File.ReadAllText(swr.Path);

            foreach (Group matchedGroup in _payloadPattern.Matches(fileContent).Select(m => m.Groups[1]))
            {
                string variableInput = matchedGroup.Value;

                // Replace known variable with actual value
                variableInput = variableInput.Replace("!(bindpath.sources)", "src");

                Match variableMatch = _variablePattern.Match(variableInput);
                if (!variableMatch.Success)
                {
                    // This payload path contains no variables. Skip
                    continue;
                }

                Group variableGroup = variableMatch.Groups[1];
                string variableName = variableGroup.Value;
                string variableValuePattern = variableInput.Substring(0, variableGroup.Index - 2) +
                    "(.*)" +
                    variableInput.Substring(variableGroup.Index + variableGroup.Length + 1);

                // Backslashes should be considered as literals in regex
                variableValuePattern = variableValuePattern.Replace("\\", "\\\\");

                swr.PayloadPaths.Add(new PayloadPath(new Regex(variableValuePattern), variableName));
            }

            return swr;
        }
    }
}
