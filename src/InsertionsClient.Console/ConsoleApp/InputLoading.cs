// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.Net.Insertions.ConsoleApp
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

            HashSet<string> ignoredPackages = new HashSet<string>();
            string[] fileLines = File.ReadAllLines(ignoredPackagesFile);

            foreach (string line in fileLines)
            {
                ignoredPackages.Add(line);
            }

            return ignoredPackages.ToImmutableHashSet();
        }
    }
}
