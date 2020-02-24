// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Api;
using Microsoft.Net.Insertions.Models;
using Microsoft.Net.Insertions.Api.Providers;
using Microsoft.Net.Insertions.Common.Constants;
using Microsoft.Net.Insertions.Common.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Microsoft.Net.Insertions.ConsoleApp
{
    internal class Program
    {
        private const string SwitchDefaultConfig = "-d:";

        private const string SwitchManifest = "-m:";

        private const string SwitchMaxWaitSeconds = "-w:";

        private const string SwitchMaxConcurrency = "-c:";

        private static readonly Lazy<string> HelpParameters = new Lazy<string>(() =>
        {
            StringBuilder txt = new StringBuilder();

            txt.Append($"{SwitchDefaultConfig}<default.config full file path>");
            txt.Append(" ");
            txt.Append($"{SwitchManifest}<manifest.json full file path>");
            txt.Append(" ");
            txt.Append($"{SwitchMaxWaitSeconds}<maximum seconds to allow job run, as int>");
            txt.Append(" ");
            txt.Append($"{SwitchMaxConcurrency}<max concurrent default.config updates, as int>");

            return txt.ToString();
        });

        private static readonly Lazy<string> ProgramName = new Lazy<string>(() => Assembly.GetExecutingAssembly().GetName().Name);

        private static string DefaultConfigFile = string.Empty;

        private static string ManifestFile = string.Empty;

        private static string MaxWaitSeconds = string.Empty;

        private static string MaxConcurrency = string.Empty;


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
        private static void Main(string[] args)
        {
            ShowStartOrEndMessage($"Running {ProgramName.Value}");

            ProcessCmdArguments(args);

            IInsertionApiFactory apiFactory = new InsertionApiFactory();
            IInsertionApi api = apiFactory.Create(MaxWaitSeconds, MaxConcurrency);
            UpdateResults results = api.UpdateVersions(ManifestFile, DefaultConfigFile);

            ShowResults(results);

            Trace.WriteLine($"Log: {LogFile}{Environment.NewLine}");
        }

        private static void ShowResults(UpdateResults results)
        {
            Console.ForegroundColor = results.Outcome ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"Completed {(results.Outcome ? "successfully" : "in a failure")}.");
            if (!results.Outcome)
            {
                Console.WriteLine($"Details: {results.OutcomeDetails}.");
            }
            Console.ResetColor();
            Trace.WriteLine($"Duration: {results.DurationMilliseconds:N2}-ms.");
            Trace.WriteLine($"Successful updates: {results.UpdatedNuGets.Count():N0}.");
            Trace.WriteLine("Updated default.config NuGet package versions...");
            foreach (string updatedNuget in results.UpdatedNuGets)
            {
                Trace.WriteLine($"           {updatedNuget}");
            }
        }

        private static void ShowStartOrEndMessage(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        private static void ShowHelp()
        {
            Console.WriteLine($"{Environment.NewLine}");
            Console.WriteLine($"{ProgramName.Value} updates versions of NuGet packages in {InsertionConstants.DefaultConfigFile} with the corresponding values from {InsertionConstants.ManifestFile}{Environment.NewLine}");

            Console.WriteLine($"Usage:");
            Console.WriteLine($">{ProgramName.Value}.exe {HelpParameters.Value}");

            Console.WriteLine($"{Environment.NewLine}Options:");
            Console.WriteLine($"{SwitchDefaultConfig}   full path on disk to default.config to update");
            Console.WriteLine($"{SwitchManifest}   full path on disk to manifest.json");
            Console.WriteLine($"{SwitchMaxWaitSeconds}   maximum allowed duration in seconds [optional]");
            Console.WriteLine($"{SwitchMaxConcurrency}   maximum concurrency of default.config version updates [optional]{Environment.NewLine}");
 
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Example...");
            Console.WriteLine($">{ProgramName.Value} {SwitchDefaultConfig}c:\\default.config {SwitchManifest}c:\\manifest.json");
            Console.ResetColor();
        }

        private static void ShowErrorHelpAndExit(string reason)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Exiting due to incorrect input.  Reason: {reason}");
            Console.ResetColor();
            ShowHelp();
            Environment.Exit(1);
        }

        private static void ProcessCmdArguments(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Processing CMD line parameters");
            Console.ResetColor();

            if (args == null || args.Length < 2)
            {
                ShowErrorHelpAndExit("incorrect # of parameters specified");
            }

            static void ProcessArgument(string argument, string appSwitch, string cmdLineMessage, ref string target)
            {
                target = argument.Replace(appSwitch, string.Empty);
                Trace.WriteLine($"CMD line param. {cmdLineMessage} {target}");
            }

            foreach (var arg in args)
            {
                if (arg.StartsWith(SwitchDefaultConfig))
                {
                    ProcessArgument(arg, SwitchDefaultConfig, $"Specified {InsertionConstants.DefaultConfigFile}: {DefaultConfigFile}", ref DefaultConfigFile);
                }
                else if (arg.StartsWith(SwitchManifest))
                {
                    ProcessArgument(arg, SwitchManifest, $"Specified {InsertionConstants.ManifestFile}: {ManifestFile}", ref ManifestFile);
                }
                else if (arg.StartsWith(SwitchMaxWaitSeconds))
                {
                    ProcessArgument(arg, SwitchMaxWaitSeconds, $"Specified \"max wait seconds\": {MaxWaitSeconds}", ref MaxWaitSeconds);
                }
                else if (arg.StartsWith(SwitchMaxConcurrency))
                {
                    ProcessArgument(arg, SwitchMaxConcurrency, $"Specified \"max concurrency\": {MaxConcurrency}", ref MaxConcurrency);
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