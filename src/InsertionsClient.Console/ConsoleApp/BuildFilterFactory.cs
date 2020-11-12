// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.InsertionsClient.ConsoleApp
{
    internal static class BuildFilterFactory
    {
        private static readonly Type TypeOfBuild = typeof(Build);

        /// <summary>
        /// Given a filter string, generates a predicate that will decide
        /// if the given build should pass or fail the filter.
        /// </summary>
        /// <param name="filterString">Input string that defines the filter.</param>
        /// <param name="buildFilter">The created predicate.</param>
        /// <returns>True if operation succeeded. False, otherwise.</returns>
        public static bool TryCreateFromString(string filterString, out Predicate<Build>? buildFilter)
        {
            buildFilter = null;

            if (string.IsNullOrWhiteSpace(filterString))
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
                ParameterExpression buildParameter = Expression.Parameter(TypeOfBuild, "build");

                string[] rulesets = filterString.Split(';');
                foreach (string ruleset in rulesets)
                {
                    if (string.IsNullOrWhiteSpace(ruleset))
                    {
                        Trace.WriteLine("Ruleset cannot be empty. Please check your build filter.");
                        return false;
                    }

                    // Create the expression that only checks this one ruleset.
                    if (!TryCreateRulesetExpression(buildParameter, ruleset, out Expression? rulesetExpression))
                    {
                        Trace.WriteLine("Failed to create the expression for ruleset: " + ruleset);
                        return false;
                    }

                    if (rootExpression == null)
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
        /// Creates an expression that checks weather a given Build successfully passes the filtering of the provided ruleset.
        /// </summary>
        /// <param name="buildParameter"></param>
        /// <param name="ruleset"></param>
        /// <param name="rulesetExpression"></param>
        /// <returns></returns>
        private static bool TryCreateRulesetExpression(ParameterExpression buildParameter, string ruleset, out Expression? rulesetExpression)
        {
            // Generated expressions from each rule in this ruleset will be combined with "And" to construct this ruleset expression.
            rulesetExpression = null;
            string[] rules = ruleset.Split(',');
            foreach (string rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule))
                {
                    Trace.WriteLine($"An empty rule was found in ruleset {ruleset}. Please check your build filter.");
                    return false;
                }

                string[] sides = rule.Split('=', 2);
                if (sides.Length != 2 || sides[0].Length == 0 || sides[1].Length == 0)
                {
                    Trace.WriteLine($"Failed to parse the rule \"{rule}\": an equals sign should split the rule into two parts.");
                    return false;
                }

                string propertyName = sides[0];
                string regularExp = sides[1];
                Regex? regex = null;

                try
                {
                    regex = new Regex(regularExp, RegexOptions.Compiled);
                }
                catch (Exception e)
                {
                    Trace.WriteLine("Failed to parse the regular expression: " + regularExp + ": " + e.ToString());
                    return false;
                }

                if (!TryGetBuildSelectorFromPropertyName(propertyName, regex, out Predicate<Build>? buildSelector)
                    || buildSelector == null)
                {
                    Trace.WriteLine("Build selector couldn't be constructed from the given property name: " + propertyName);
                    return false;
                }

                MethodCallExpression ruleExpression = Expression.Call(Expression.Constant(buildSelector.Target), buildSelector.Method, buildParameter);

                if (rulesetExpression == null)
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

            return true;
        }

        /// <summary>
        /// Attempts to create a predicate with the given property name and the regular expression.
        /// The predicate (1) takes a build as argument,
        /// (2) accesses the property with the given name,
        /// (3) tries to match it with the provided regular expression,
        /// and (4) returns the result of the match.
        /// </summary>
        /// <param name="propertyName">Name of the property in the <see cref="Build"/> instance to be inputted to regex match.</param>
        /// <param name="regex">Regular expression to test the property with.</param>
        /// <param name="buildSelector">The generated predicate that can be used to check if a build succeeds matching its
        /// property with the regular expression.</param>
        /// <returns>True if creating a predicate was successful. False, otherwise.</returns>
        private static bool TryGetBuildSelectorFromPropertyName(string propertyName, Regex regex, out Predicate<Build>? buildSelector)
        {
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
                    buildSelector = CreateBuildPredicateBasedOnChannel(c => c.Name, regex);
                    break;
                case "channelId":
                    buildSelector = CreateBuildPredicateBasedOnChannel(c => c.Id.ToString(), regex);
                    break;
                default:
                    buildSelector = null;
                    Trace.WriteLine($"Given value \"{propertyName}\" does not correspond to a valid build property.");
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Creates a predicate that checks if the given property matches the provided regular expression.
        /// </summary>
        /// <param name="propertyGetter">Getter that will return a string property from a <see cref="Build"/></param>
        /// <param name="regex">Regular expression to be used for evaluating the value of the property.</param>
        /// <returns>The created predicate.</returns>
        private static Predicate<Build> CreateBuildPredicateBasedOnStringProperty(Func<Build, string?> propertyGetter, Regex regex)
        {
            return (build) =>
            {
                string value = propertyGetter(build) ?? "";
                Match? match = regex.Match(value);

                if (match == null || !match.Success)
                {
                    return false;
                }

                return match.Length == (value?.Length ?? 0);
            };
        }

        /// <summary>
        /// Creates a predicate that checks if the provided regex matches the selected property of any of the channels of the given build.
        /// </summary>
        /// <param name="channelPropertyGetter">Function to select a string from a channel for regex matching.</param>
        /// <param name="regex">Regular expression to be used for evaluating the names of build channels.</param>
        /// <returns>The created predicate.</returns>
        private static Predicate<Build> CreateBuildPredicateBasedOnChannel(Func<Channel, string?> channelPropertyGetter, Regex regex)
        {
            return (build) =>
            {
                if (build.Channels == null || build.Channels.Count == 0)
                {
                    // We couldn't find a channel matching this regular expression
                    return false;
                }

                foreach (Channel? channel in build.Channels)
                {
                    if (channel == null)
                    {
                        // Null channel cannot be considered a match.
                        continue;
                    }

                    string propertyValue = channelPropertyGetter(channel) ?? "";
                    Match? match = regex.Match(propertyValue);

                    if (match == null || !match.Success)
                    {
                        return false;
                    }

                    return match.Length == propertyValue.Length;
                }

                return false;
            };
        }
    }
}
