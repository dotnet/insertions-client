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
            if(string.IsNullOrWhiteSpace(manifestPaths))
            {
                invalidPathCount = 0;
                return new List<string>();
            }

            invalidPathCount = 0;
            List<string> results = new List<string>();
            string[] paths = manifestPaths.Split(';');

            foreach (string path in paths)
            {
                if(string.IsNullOrWhiteSpace(path))
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
    
        /// <summary>
        /// Parses the input string into a filter that will decide if a build should be inserted or not.
        /// </summary>
        /// <param name="filterString">Input string to be parsed.</param>
        /// <param name="buildFilter">Filter generated from the input string.</param>
        /// <returns>True if loading succeeded. False otherwise. </returns>
        internal static bool LoadBuildFilter(string filterString, out Predicate<Build>? buildFilter)
        {
            buildFilter = null;

            if(string.IsNullOrWhiteSpace(filterString))
            {
                Trace.WriteLine("Failed to parse build filter. Provided input string is empty.");
                return false;
            }

            try
            {
                // Root expression that the final predicate will be build on.
                // Generated expressions from each ruleset will be combined with "Or" to construct this root expression.
                Expression? rootExpression = null;

                // Parameter for the build that we pass to the filter i.e. the parameter for the final predicate.
                ParameterExpression buildParameter = Expression.Parameter(typeof(Build), "build");

                string[] rulesets = filterString.Split(';');
                foreach(string ruleset in rulesets)
                {
                    if(string.IsNullOrWhiteSpace(ruleset))
                    {
                        Trace.WriteLine("Ruleset cannot be empty. Please check your build filter.");
                        return false;
                    }

                    // Expression that only checks this one ruleset.
                    // Generated expressions from each rule in this ruleset will be combined with "And" to construct this ruleset expression.
                    Expression? rulesetExpression = null;

                    string[] rules = ruleset.Split(',');
                    foreach(string rule in rules)
                    {
                        if (string.IsNullOrWhiteSpace(rule))
                        {
                            Trace.WriteLine($"An empty rule was found in ruleset {ruleset}. Please check your build filter.");
                            return false;
                        }

                        string[] sides = rule.Split('=', 2);
                        if(sides.Length != 2)
                        {
                            Trace.WriteLine($"Failed to parse the rule \"{rule}\": an equals sign should split the rule into two parts.");
                            return false;
                        }

                        string propertyName = sides[0];
                        string regularExp = sides[1];

                        Regex regex = new Regex(regularExp, RegexOptions.Compiled);
                        Predicate<Build>? buildSelector = null;

                        switch (propertyName)
                        {
                            case "repo":
                                buildSelector = CreateBuildPredicateBasedOnStringProperty(b => b.Repository, regex);
                                break;
                            case "commit":
                                buildSelector = CreateBuildPredicateBasedOnStringProperty(b => b.Commit, regex);
                                break;
                            case "branch":
                                buildSelector = CreateBuildPredicateBasedOnStringProperty(b => b.Branch, regex);
                                break;
                            case "buildNumber":
                                buildSelector = CreateBuildPredicateBasedOnStringProperty(b => b.BuildNumber, regex);
                                break;
                            case "channel":
                                buildSelector = CreateBuildPredicateBasedOnChannel(regex);
                                break;
                            default:
                                Trace.WriteLine($"Given value \"{propertyName}\" does not correspond to a valid build property.");
                                return false;
                        }

                        MethodCallExpression ruleExpression = Expression.Call(Expression.Constant(buildSelector.Target), buildSelector.Method, buildParameter);

                        if(rulesetExpression == null)
                        {
                            // We haven't put any code into the expression of this ruleset. This will be the first.
                            rulesetExpression = ruleExpression;
                        }
                        else
                        {
                            // This ruleset already contains some code from the previous rules. Combine them with "And".
                            rulesetExpression = Expression.AndAlso(rulesetExpression, ruleExpression);
                        }
                    }

                    if(rootExpression == null)
                    {
                        // We haven't put any code into the root expression. This will be the first.
                        rootExpression = rulesetExpression;
                    }
                    else
                    {
                        // Root expression already contains some code from the previous rulesets. Combine the new one with "Or".
                        rootExpression = Expression.OrElse(rootExpression, rulesetExpression);
                    }

                }

                Expression<Predicate<Build>> lambda = Expression.Lambda<Predicate<Build>>(rootExpression, false, buildParameter);
                buildFilter = lambda.Compile();
                return true;
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Parsing the build filter has failed unexpectedly. Exception: {e.ToString()}");
            }

            return false;
        }

        /// <summary>
        /// Creates a predicate that checks if the given property matches the provided regular expression.
        /// </summary>
        /// <param name="propertyGetter">Getter that will return a string property from a <see cref="Build"/></param>
        /// <param name="regex">Regular expression to be used for evaluating the value of the property.</param>
        /// <returns>The created predicate.</returns>
        internal static Predicate<Build> CreateBuildPredicateBasedOnStringProperty(Func<Build, string?> propertyGetter, Regex regex)
        {
            return (build) =>
            {
                string? value = propertyGetter(build);
                Match? match = regex.Match(value);

                if (match == null || !match.Success)
                {
                    return false;
                }

                return match.Length == (value?.Length ?? 0);
            };
        }

        /// <summary>
        /// Creates a predicate that checks if the provided regex matches any of the channels of the given build.
        /// </summary>
        /// <param name="regex">Regular expression to be used for evaluating the names of build channels.</param>
        /// <returns>The created predicate.</returns>
        internal static Predicate<Build> CreateBuildPredicateBasedOnChannel(Regex regex)
        {
            return (build) =>
            {
                if(build.Channels == null || build.Channels.Count == 0)
                {
                    // We couldn't find a channel matching this regular expression
                    return false;
                }

                foreach(Channel? channel in build.Channels)
                {
                    if(channel == null)
                    {
                        // Null channel cannot be considered a match.
                        continue;
                    }

                    Match? match = regex.Match(channel.Name);

                    if (match == null || !match.Success)
                    {
                        return false;
                    }

                    return match.Length == (channel.Name?.Length ?? 0);
                }

                return false;
            };
        }
    }
}
