// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Api;
using Microsoft.DotNet.InsertionsClient.Api.Providers;
using Microsoft.DotNet.InsertionsClient.Common.Constants;
using Microsoft.DotNet.InsertionsClient.Common.Logging;
using Microsoft.DotNet.InsertionsClient.Models;
using Microsoft.DotNet.InsertionsClient.Props.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("InsertionsClient.Console.Test")]
namespace Microsoft.DotNet.InsertionsClient.ConsoleApp
{
    internal class Program
    {
        private const string SwitchDefaultConfig = "-d:";

        private const string SwitchManifest = "-m:";

        private const string SwitchWhitelistedPackages = "-wl:";

        private const string SwitchIgnorePackages = "-i:";

        private const string SwitchIgnoreDevUxTeamPackages = "-idut";

        private const string SwitchPropsFilesRootDir = "-p:";

        private const string SwitchFeedAccessToken = "-a:";

        private const string SwitchMaxWaitSeconds = "-w:";

        private const string SwitchMaxDownloadSeconds = "-ds";

        private const string SwitchMaxConcurrency = "-c:";

        private static readonly Lazy<string> HelpParameters = new Lazy<string>(() =>
        {
            StringBuilder txt = new StringBuilder();

            txt.Append($"{SwitchDefaultConfig}<default.config full file path>");
            txt.Append(" ");
            txt.Append($"{SwitchManifest}<manifest.json full file path>");
            txt.Append(" ");
            txt.Append($"[{SwitchWhitelistedPackages}<whitelisted packages file path>]");
            txt.Append(" ");
            txt.Append($"[{SwitchIgnorePackages}<ignored packages file path>]");
            txt.Append(" ");
            txt.Append($"[{SwitchPropsFilesRootDir}<root directory that contains props files>]");
            txt.Append(" ");
            txt.Append($"[{SwitchFeedAccessToken}<token to access nuget feed>]");
            txt.Append(" ");
            txt.Append($"[{SwitchMaxWaitSeconds}<maximum seconds to allow job run, excluding downloads, as int>]");
            txt.Append(" ");
            txt.Append($"[{SwitchMaxDownloadSeconds}<maximum seconds to allow nuget downloads run, as int>]");
            txt.Append(" ");
            txt.Append($"[{SwitchMaxConcurrency}<max concurrent default.config updates, as int>]");

            return txt.ToString();
        });

        private static readonly Lazy<string> ProgramName = new Lazy<string>(() => Assembly.GetExecutingAssembly().GetName().Name!);

        private static string DefaultConfigFile = string.Empty;

        private static string ManifestFile = string.Empty;

        private static string WhitelistedPackagesFile = string.Empty;

        private static string IgnoredPackagesFile = string.Empty;

        private static bool IgnoreDevUxTeamPackagesScenario;

        private static string PropsFilesRootDirectory = string.Empty;

        private static string? FeedAccessToken = null;

        private static TimeSpan MaxWaitDuration = TimeSpan.FromSeconds(75);

        private static TimeSpan MaxDownloadDuration = TimeSpan.FromSeconds(240);

        private static int MaxConcurrency = 4;


        /// <summary>
        /// Sets up logging based on the TRACE switch being set.
        /// </summary>
        static Program()
        {
            string logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            LogFile = Path.Combine(logDirectory, $"log_{DateTime.Now.Ticks}.txt");

            Trace.AutoFlush = true;
            _ = Trace.Listeners.Add(new InsertionsTextWriterTraceListener(LogFile, "tracelistener"));
            _ = Trace.Listeners.Add(new InsertionsConsoleTraceListener());
        }


        private static string LogFile { get; }

        [STAThread]
        private static int Main(string[] args)
        {
            ShowStartOrEndMessage($"Running {ProgramName.Value}");

            ProcessCmdArguments(args);

            IInsertionApiFactory apiFactory = new InsertionApiFactory();
            IInsertionApi api = apiFactory.Create(MaxWaitDuration, MaxDownloadDuration, MaxConcurrency);
            List<string> manifestFiles = InputLoading.LoadManifestPaths(ManifestFile, out int invalidManifestFileCount);
            IEnumerable<Regex> whitelistedPackages = InputLoading.LoadWhitelistedPackages(WhitelistedPackagesFile);
            ImmutableHashSet<string> ignoredPackages = ImmutableHashSet<string>.Empty;

            if(invalidManifestFileCount != 0)
            {
                ShowErrorHelpAndExit($"Failed to find one or more manifest.json files specified in '{ManifestFile}'");
            }

            if (!string.IsNullOrWhiteSpace(IgnoredPackagesFile))
            {
                ignoredPackages = InputLoading.LoadPackagesToIgnore(IgnoredPackagesFile);
            }
            else if (IgnoreDevUxTeamPackagesScenario)
            {
                ignoredPackages = InsertionConstants.DefaultDevUxTeamPackages;
            }
            
            UpdateResults results = api.UpdateVersions(
                    manifestFiles,
                    DefaultConfigFile,
                    whitelistedPackages,
                    ignoredPackages,
                    FeedAccessToken,
                    PropsFilesRootDirectory
                );

            ShowResults(results);

            Trace.WriteLine($"Log: {LogFile}{Environment.NewLine}");

            return results.Outcome ? 0 : 1;
        }

        private static void ShowResults(UpdateResults results)
        {
            Console.ForegroundColor = results.Outcome ? ConsoleColor.Green : ConsoleColor.Red;
            Trace.WriteLine($"Completed {(results.Outcome ? "successfully" : "in a failure")}.");
            if (!results.Outcome)
            {
                Trace.WriteLine($"Details: {results.OutcomeDetails}.");
            }
            Console.ResetColor();
            Trace.WriteLine($"Duration: {results.DurationMilliseconds:N2}-ms.");
            Trace.WriteLine($"Successful updates: {results.UpdatedNuGets.Count():N0}.");
            Trace.WriteLine("Found package version changes in config files...");
            foreach (PackageUpdateResult updatedNuget in results.UpdatedNuGets.OrderBy(r => r.PackageId))
            {
                Trace.WriteLine($"           {updatedNuget.PackageId}: {updatedNuget.NewVersion}");
            }

            Trace.WriteLine($"Updated {results.FileSaveResults?.Length ?? 0} config files...");
            foreach (FileSaveResult configSaveResult in results.FileSaveResults ?? Enumerable.Empty<FileSaveResult>())
            {
                if (configSaveResult.Succeeded)
                {
                    Trace.WriteLine($"           Saved: {configSaveResult.Path}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Trace.WriteLine($"           Save Failed: {configSaveResult.Path}");
                    Console.ResetColor();
                }
            }


            if (results.PropsFileUpdateResults != null)
            {
                Console.ForegroundColor = results.PropsFileUpdateResults.Outcome ? ConsoleColor.Green : ConsoleColor.Red;
                Trace.WriteLine($"Props file updating completed {(results.PropsFileUpdateResults.Outcome ? "successfully" : "in a failure")}.");
                if (!results.PropsFileUpdateResults.Outcome)
                {
                    Trace.WriteLine($"Details: {results.PropsFileUpdateResults.OutcomeDetails}.");
                }
                Console.ResetColor();
                Trace.WriteLine($"Updated {results.PropsFileUpdateResults.UpdatedVariables.Count} .props files:");
                foreach (KeyValuePair<PropsFile, List<PropsFileVariableReference>> propsFile in results.PropsFileUpdateResults.UpdatedVariables.Where(r => r.Value.Count != 0).OrderBy(p => p.Key.Path))
                {
                    Trace.WriteLine($"        {propsFile.Key.Path}");
                    foreach (PropsFileVariableReference? variableChange in propsFile.Value)
                    {
                        Trace.WriteLine($"                {variableChange.Name}={variableChange.Value}");
                    }
                }

                if (results.PropsFileUpdateResults.UnrecognizedVariables.Count != 0)
                {
                    Trace.WriteLine($"{results.PropsFileUpdateResults.UnrecognizedVariables.Count} variables were not found in props files:");
                    foreach (PropsFileVariableReference? variable in results.PropsFileUpdateResults.UnrecognizedVariables)
                    {
                        Trace.WriteLine($"        {variable.Name} in {variable.ReferencedFilePath}");
                    }
                }
            }
        }

        private static void ShowStartOrEndMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Trace.WriteLine(message);
            Console.ResetColor();
        }

        private static void ShowHelp()
        {
            Trace.WriteLine($"{Environment.NewLine}");
            Trace.WriteLine($"{ProgramName.Value} updates versions of NuGet packages in {InsertionConstants.DefaultConfigFile} with the corresponding values from {InsertionConstants.ManifestFile}{Environment.NewLine}." +
                $" If an access token is specified with the switch {SwitchFeedAccessToken}, properties defined in .props files are also updated.");

            Trace.WriteLine($"Usage:");
            Trace.WriteLine($">{ProgramName.Value}.exe {HelpParameters.Value}");

            Trace.WriteLine($"{Environment.NewLine}Options:");
            Trace.WriteLine($"{SwitchDefaultConfig}   path on disk to default.config to update");
            Trace.WriteLine($"{SwitchManifest}   path on disk to a manifest.json file or to the containing folder. Supports multiple entries separated by semicolons");
            Trace.WriteLine($"{SwitchWhitelistedPackages}   full path on disk to whitelisted packages file. Each line should contain a regex pattern that may match zero or more package ids [optional]");
            Trace.WriteLine($"{SwitchIgnorePackages}   full path on disk to ignored packages file. Each line should have a package id [optional]");
            Trace.WriteLine($"{SwitchPropsFilesRootDir}   directory to search for and update .props files [optional]");
            Trace.WriteLine($"{SwitchFeedAccessToken}   token to access nuget feed. Necessary when updating props files [optional]");
            Trace.WriteLine($"{SwitchMaxWaitSeconds}   maximum allowed duration in seconds, excluding downloads [optional]");
            Trace.WriteLine($"{SwitchMaxDownloadSeconds}   maximum allowed duration in seconds that can be spent downloading nuget packages [optional]");
            Trace.WriteLine($"{SwitchMaxConcurrency}   maximum concurrency of default.config version updates [optional]{Environment.NewLine}");

            Console.ForegroundColor = ConsoleColor.Green;
            Trace.WriteLine("Example...");
            Trace.WriteLine($">{ProgramName.Value} {SwitchDefaultConfig}c:\\default.config {SwitchManifest}c:\\manifest.json");
            Console.ResetColor();
        }

        private static void ShowErrorHelpAndExit(string reason)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Trace.WriteLine($"Exiting due to incorrect input.  Reason: {reason}");
            Console.ResetColor();
            ShowHelp();
            Environment.Exit(1);
        }

        private static void ProcessCmdArguments(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Trace.WriteLine("Processing CMD line parameters");
            Console.ResetColor();

            if (args == null || args.Length < 2)
            {
                ShowErrorHelpAndExit("incorrect # of parameters specified");
            }

            foreach (string arg in args!)
            {
                if (arg.StartsWith(SwitchDefaultConfig))
                {
                    DefaultConfigFile = InputLoading.ProcessArgument(arg, SwitchDefaultConfig, $"Specified {InsertionConstants.DefaultConfigFile}:");
                }
                else if (arg.StartsWith(SwitchManifest))
                {
                    ManifestFile = InputLoading.ProcessArgument(arg, SwitchManifest, $"Specified {InsertionConstants.ManifestFile}:");
                }
                else if (arg.StartsWith(SwitchWhitelistedPackages))
                {
                    WhitelistedPackagesFile = InputLoading.ProcessArgument(arg, SwitchWhitelistedPackages, $"Specified whitelisted packages file:");
                }
                else if (arg.StartsWith(SwitchIgnorePackages))
                {
                    IgnoredPackagesFile = InputLoading.ProcessArgument(arg, SwitchIgnorePackages, $"Specified ignored packages file:");
                }
                else if (arg.StartsWith(SwitchPropsFilesRootDir))
                {
                    PropsFilesRootDirectory = InputLoading.ProcessArgument(arg, SwitchPropsFilesRootDir, $"Specified root directory for props files:");
                }
                else if (arg.StartsWith(SwitchFeedAccessToken))
                {
                    FeedAccessToken = arg.Replace(SwitchFeedAccessToken, string.Empty);
                    Trace.WriteLine($"CMD line param. An access token was specified.");
                }
                else if (arg.StartsWith(SwitchMaxWaitSeconds))
                {
                    int waitDurationSeconds = InputLoading.ProcessArgumentInt(arg, SwitchMaxWaitSeconds, $"Specified \"max run duration in seconds, excluding downloads\":");
                    MaxWaitDuration = TimeSpan.FromSeconds(waitDurationSeconds);
                }
                else if (arg.StartsWith(SwitchMaxDownloadSeconds))
                {
                    int downloadDurationSeconds = InputLoading.ProcessArgumentInt(arg, SwitchMaxDownloadSeconds, $"Specified \"max download duration in seconds\":");
                    MaxDownloadDuration = TimeSpan.FromSeconds(downloadDurationSeconds);
                }
                else if (arg.StartsWith(SwitchMaxConcurrency))
                {
                    MaxConcurrency = InputLoading.ProcessArgumentInt(arg, SwitchMaxConcurrency, $"Specified \"max concurrency\":");
                }
                else if (arg.StartsWith(SwitchIgnoreDevUxTeamPackages))
                {
                    IgnoreDevUxTeamPackagesScenario = true;
                }
            }

            if (string.IsNullOrWhiteSpace(DefaultConfigFile))
            {
                ShowErrorHelpAndExit($"{InsertionConstants.DefaultConfigFile} path not set.");
            }
            if (string.IsNullOrWhiteSpace(ManifestFile))
            {
                ShowErrorHelpAndExit($"{InsertionConstants.ManifestFile} path not set.");
            }
        }
    }
}