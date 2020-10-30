// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Api;
using Microsoft.DotNet.InsertionsClient.Common.Constants;
using Microsoft.DotNet.InsertionsClient.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.InsertionsClient.ConsoleApp
{
    internal static class InputLoading
    {
        internal static string ProcessArgument(string argument, string appSwitch, string cmdLineMessage)
        {
            string argumentValue = argument.Replace(appSwitch, string.Empty);
            Trace.WriteLine($"CMD line param. {cmdLineMessage} {argumentValue}");
            return argumentValue;
        }

        internal static int ProcessArgumentInt(string argument, string appSwitch, string cmdLineMessage)
        {
            string trimmedArg = argument.Replace(appSwitch, string.Empty);
            if (int.TryParse(trimmedArg, out int argumentValue))
            {
                Trace.WriteLine($"CMD line param. {cmdLineMessage} {argumentValue}");
                return argumentValue;
            }

            Trace.WriteLine(@"Specified value is not an integer. Default value of ""-1"" will be used.");
            return -1;
        }

        internal static IEnumerable<Regex> LoadWhitelistedPackages(string whitelistedPackagesFile)
        {
            if (!File.Exists(whitelistedPackagesFile))
            {
                return Enumerable.Empty<Regex>();
            }

            try
            {
                return File.ReadAllLines(whitelistedPackagesFile)
                    .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled))
                    .ToList();
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Failed to load whitelisted packages file. An empty whitelist will be used instead. Exception:{e.ToString()}");
                return Enumerable.Empty<Regex>();
            }
        }

        internal static ImmutableHashSet<string> LoadPackagesToIgnore(string ignoredPackagesFile)
        {
            if (!File.Exists(ignoredPackagesFile))
            {
                return ImmutableHashSet<string>.Empty;
            }

            return File.ReadAllLines(ignoredPackagesFile).ToImmutableHashSet();
        }

        /// <summary>
        /// Parses the semicolon separated manifest path string into multiple file paths.
        /// In the case that a path is pointing to a directory instead of a file, manifest file
        /// is searched in that directory.
        /// </summary>
        /// <param name="manifestPaths">Semicolon separated String that contains all the paths.</param>
        /// <param name="invalidPathCount">Number of paths that failed to resolve to a file on disk.</param>
        /// <returns>A list of paths pointing to manifest files.</returns>
        internal static List<string> LoadManifestPaths(string manifestPaths, out int invalidPathCount)
        {
            if (string.IsNullOrWhiteSpace(manifestPaths))
            {
                invalidPathCount = 0;
                return new List<string>();
            }

            invalidPathCount = 0;
            List<string> results = new List<string>();
            string[] paths = manifestPaths.Split(';');

            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                string pathToFile = path;

                if (Directory.Exists(path))
                {
                    // Path appears to be a directory. Manifest should be inside.
                    pathToFile = Path.Combine(path, InsertionConstants.ManifestFile);
                }

                if (File.Exists(pathToFile))
                {
                    results.Add(pathToFile);
                    continue;
                }

                invalidPathCount++;
                Trace.WriteLine($"The given path neither points to a manifest file nor to a directory that contains a manifest file: {path}");
            }

            return results;
        }
    }
}
