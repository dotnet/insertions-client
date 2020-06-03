// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Api.Props.Models;
using Microsoft.DotNet.InsertionsClient.Common.Constants;
using Microsoft.DotNet.InsertionsClient.Common.Json;
using Microsoft.DotNet.InsertionsClient.Models;
using Microsoft.DotNet.InsertionsClient.Models.Extensions;
using Microsoft.DotNet.InsertionsClient.Props.Models;
using Microsoft.DotNet.InsertionsClient.Telemetry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.InsertionsClient.Api.Providers
{
    /// <summary>
    /// <see cref="IInsertionApi"/> provider relying on <see cref="IDefaultConfigApi"/> instances to update NuGet versions.
    /// </summary>
    internal sealed class InsertionApi : IInsertionApi
    {
        private readonly MeasurementsSession _metrics;

        private readonly int _maxConcurrentWorkers;

        private readonly TimeSpan _maxWaitDuration;

        private readonly TimeSpan _maxDownloadDuration;

        internal InsertionApi(TimeSpan? maxWaitSeconds = null, TimeSpan? maxDownloadSeconds = null, int? maxConcurrency = null)
        {
            _metrics = new MeasurementsSession();
            _maxWaitDuration = TimeSpan.FromSeconds(Math.Max(maxWaitSeconds?.TotalSeconds ?? 120, 60));
            _maxDownloadDuration = TimeSpan.FromSeconds(Math.Max(maxDownloadSeconds?.TotalSeconds ?? 240, 1));
            _maxConcurrentWorkers = Math.Clamp(maxConcurrency ?? 20, 1, 20);
        }


        #region IInsertionApi API

        public UpdateResults UpdateVersions(
            string manifestFile,
            string defaultConfigFile,
            IEnumerable<Regex> whitelistedPackages,
            ImmutableHashSet<string>? packagesToIgnore,
            string? accessToken = null,
            string? propsFilesRootDirectory = null)
        {
            List<Asset> assets = null!;
            DefaultConfigUpdater configUpdater;

            if (!TryValidateManifestFile(manifestFile, out string details)
                || !TryExtractManifestAssets(manifestFile, out assets, out details)
                || !TryLoadDefaultConfig(defaultConfigFile, out configUpdater, out details))
            {
                return new UpdateResults { OutcomeDetails = details };
            }

            UpdateResults results = new UpdateResults
            {
                IgnoredNuGets = packagesToIgnore
            };
            Stopwatch overallRunStopWatch = Stopwatch.StartNew();
            using CancellationTokenSource source = new CancellationTokenSource(_maxWaitDuration);
            try
            {
                _ = Parallel.ForEach(assets,
                    CreateParallelOptions(source.Token),
                    asset => ParallelCallback(asset, whitelistedPackages, packagesToIgnore, configUpdater, results));

                /* Delay saving config file changes until props file updates are successful.
                 * If we save the results now and props-file step fails, re-running the application won't attempt to update props files again.
                 * A partial success in the app shouldn't hide the errors in the consecutive runs. */

                // Only update props files if user specified an access token. Null token means user doesn't want to update props files.
                bool propsUpdatesEnabled = accessToken != null;

                if (propsUpdatesEnabled)
                {
                    // Attempt to find a proper directory to search for props files, if we are not already given one.
                    if (string.IsNullOrWhiteSpace(propsFilesRootDirectory))
                    {
                        if (FindPropsFileRootDirectory(defaultConfigFile, out propsFilesRootDirectory))
                        {
                            Trace.WriteLine($"The directory to search for .props files: {propsFilesRootDirectory}");
                        }
                        else
                        {
                            Trace.WriteLine("Failed to find an appropriate folder to search for .props files.");

                            results.PropsFileUpdateResults = new PropsUpdateResults()
                            {
                                Outcome = false,
                                OutcomeDetails = "Failed to find an appropriate folder to search for .props files."
                            };
                        }
                    }

                    // Update props files if we have a valid directory to search
                    if (!string.IsNullOrWhiteSpace(propsFilesRootDirectory))
                    {
                        SwrFileReader swrFileReader = new SwrFileReader(_maxConcurrentWorkers);
                        SwrFile[] swrFiles = swrFileReader.LoadSwrFiles(propsFilesRootDirectory);

                        PropsVariableDeducer variableDeducer = new PropsVariableDeducer(InsertionConstants.DefaultNugetFeed, accessToken);
                        bool deduceOperationResult = variableDeducer.DeduceVariableValues(configUpdater, results.UpdatedNuGets,
                            swrFiles, out List<PropsFileVariableReference> variables, out string outcomeDetails, _maxDownloadDuration);

                        PropsFileUpdater propsFileUpdater = new PropsFileUpdater();
                        results.PropsFileUpdateResults = propsFileUpdater.UpdatePropsFiles(variables, propsFilesRootDirectory);

                        if (!deduceOperationResult)
                        {
                            results.PropsFileUpdateResults.Outcome = false;
                            results.PropsFileUpdateResults.OutcomeDetails += outcomeDetails;
                        }
                    }
                }
                else
                {
                    Trace.WriteLine(".props file updates are skipped since no access token was specified.");
                }

                if (!propsUpdatesEnabled || results.PropsFileUpdateResults!.Outcome == true)
                {
                    // Prop files were updated successfuly. It is safe to save config update results.
                    results.FileSaveResults = configUpdater.Save();
                }
                else
                {
                    Trace.WriteLine("default.config and .packageconfig file updates were skipped, because " +
                        "there was an issue updating .props files.");
                    results.FileSaveResults = new FileSaveResult[0];
                }
            }
            catch (Exception e)
            {
                results.OutcomeDetails = e.Message;
            }
            finally
            {
                results.DurationMilliseconds = overallRunStopWatch.ElapsedMilliseconds;
                LogStatistics();
            }
            return results;
        }
        #endregion

        private void LogStatistics()
        {
            StringBuilder stringBuilder = new StringBuilder(900 /* rough size of stats */);
            stringBuilder.AppendLine("Statistics:");
            foreach (Update update in Enum.GetValues(typeof(Update)).OfType<Update>())
            {
                stringBuilder.AppendLine($"{update} - {update.GetString()}{Environment.NewLine}{_metrics[update]}");
            }

            Trace.Write(stringBuilder.ToString());
        }

        internal bool TryValidateManifestFile(string manifestFile, out string details)
        {
            details = string.Empty;
            if (string.IsNullOrWhiteSpace(manifestFile))
            {
                details = $"Null argument ({nameof(manifestFile)})";
            }
            else if (!File.Exists(manifestFile))
            {
                details = $"Inexistent file ({manifestFile})";
            }
            return string.IsNullOrWhiteSpace(details);
        }

        internal bool TryExtractManifestAssets(string manifestFile, out List<Asset> assets, out string details)
        {
            details = string.Empty;

            try
            {
                Manifest? buildManifest = DeserializeManifest(manifestFile);

                if (buildManifest == null)
                {
                    assets = new List<Asset>();
                    details = "Failed to read/deserialize manifest file";
                    return false;
                }

                if (!buildManifest.Validate())
                {
                    assets = new List<Asset>();
                    details = $"Validation of de-serialized {InsertionConstants.ManifestFile} file content failed.";
                    return false;
                }

                if (buildManifest.Builds == null || buildManifest.Builds.Count == 0)
                {
                    assets = new List<Asset>();
                    details = $"Manifest file contains no builds.";
                    return false;
                }

                Trace.WriteLine($"De-serialized {buildManifest.Builds.Count} builds from {InsertionConstants.ManifestFile}.");

                ConcurrentDictionary<string, Asset> map = new ConcurrentDictionary<string, Asset>();
                foreach (Build build in buildManifest.Builds.AsParallel())
                {
                    foreach (Asset asset in build.Assets.AsParallel())
                    {
                        if (string.IsNullOrWhiteSpace(asset.Name))
                        {
                            Trace.WriteLine($"Manifest file contains an asset with null/empty name: {InsertionConstants.ManifestFile}");
                            continue;
                        }

                        if (!map.TryAdd(asset.Name, asset))
                        {
                            Trace.WriteLine($"Duplicate entry in the specified {InsertionConstants.ManifestFile} for asset {asset.Name}.");
                        }
                    }
                }

                assets = new List<Asset>(map.Count);
                foreach (Asset value in map.Values.OrderBy(x => x.Name))
                {
                    assets.Add(value);
                }

                if (map.Count < 1)
                {
                    details = $"No assets in {InsertionConstants.ManifestFile}";
                }
            }
            catch (Exception e)
            {
                assets = new List<Asset>();
                details = e.Message;
            }
            return string.IsNullOrWhiteSpace(details);
        }

        private Manifest? DeserializeManifest(string manifestFile)
        {
            string json = File.ReadAllText(manifestFile);
            if (string.IsNullOrWhiteSpace(json))
            {
                Trace.WriteLine($"Failed to read {InsertionConstants.ManifestFile} JSon content from file {manifestFile}.");
                return null;
            }
            try
            {
                return Serializer.Deserialize<Manifest>(json);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Failed to de-serialize {InsertionConstants.ManifestFile} file content. Reason: {e.Message}.");
                return null;
            }
        }

        private bool TryLoadDefaultConfig(string defaultConfigPath, out DefaultConfigUpdater configUpdater, out string details)
        {
            configUpdater = new DefaultConfigUpdater();
            return configUpdater.TryLoad(defaultConfigPath, out details);
        }

        private void ParallelCallback(
            Asset asset,
            IEnumerable<Regex> whitelistedPackages,
            ImmutableHashSet<string>? packagesToIgnore,
            DefaultConfigUpdater configUpdater,
            UpdateResults results)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();

            if (!TryGetPackageId(asset.Name!, asset.Version!, out string packageId))
            {
                _metrics.AddMeasurement(Update.NotAPackage, stopWatch.ElapsedTicks);
                return;
            }

            // Whitelist isn't empty, but packageId doesn't match with any of the entries.
            if (whitelistedPackages.Any() && whitelistedPackages.All(pattern => !pattern.IsMatch(packageId)))
            {
                _metrics.AddMeasurement(Update.Ignored, stopWatch.ElapsedTicks);
                Trace.WriteLine($"Skipping {packageId} since it isn't in the whitelisted packages.");
                return;
            }

            if (packagesToIgnore != null && packagesToIgnore.Contains(packageId))
            {
                _metrics.AddMeasurement(Update.Ignored, stopWatch.ElapsedTicks);
                Trace.WriteLine($"Skipping {packageId} since it was requested to be ignored.");
                return;
            }

            if (!configUpdater.TryUpdatePackage(packageId, asset.Version!, out string oldVersion))
            {
                _metrics.AddMeasurement(Update.NoMatch, stopWatch.ElapsedTicks);
                return;
            }

            _metrics.AddMeasurement(Update.ExactMatch, stopWatch.ElapsedTicks);

            if (oldVersion != asset.Version)
            {
                results.AddPackage(new PackageUpdateResult(packageId, oldVersion, asset.Version!));
                Trace.WriteLine($"Package {packageId} was updated to version {asset.Version}");
            }
        }

        private bool TryGetPackageId(string assetName, string version, out string packageId)
        {
            packageId = string.Empty;

            if (!assetName.EndsWith(".nupkg"))
            {
                // Anything that is not a nupkg must be an exact match
                if (assetName.Contains('/') || assetName.Contains('\\'))
                {
                    // Exact matches can't have paths.
                    return false;
                }

                packageId = assetName;
                return true;
            }

            if (assetName.EndsWith(".symbols.nupkg"))
            {
                // Symbol.nupkg files should never be matched
                return false;
            }

            // We have a nupkg file path.
            string filename = Path.GetFileNameWithoutExtension(assetName);

            if (!string.IsNullOrWhiteSpace(version) &&
                filename.EndsWith(version) &&
                filename.Length > version.Length &&
                filename[filename.Length - 1 - version.Length] == '.')
            {
                // Package id with a version suffix. Remove version including the dot inbetween.
                packageId = filename.Substring(0, filename.Length - version.Length - 1);
                return true;
            }

            int index = 0;
            int versionNumberStart = -1;
            while (index < filename.Length)
            {
                char c = filename[index++];

                if (c > '0' && c <= '9')
                {
                    continue;
                }

                if (c == '.')
                {
                    if (versionNumberStart == -1)
                    {
                        // This filename starts with version numbers.
                        return false;
                    }

                    // Character block that contains only numericals. This is where the version number starts
                    packageId = filename.Substring(0, versionNumberStart);
                    return true;
                }

                // we found a letter. This cannot be the start of the version number. Skip ahead
                while (index < filename.Length && filename[index] != '.')
                {
                    index++;
                }

                versionNumberStart = index++;
            }

            // This whole asset name doesn't contain version numbers
            packageId = filename;
            return true;
        }

        private ParallelOptions CreateParallelOptions(CancellationToken token)
        {
            return new ParallelOptions
            {
                CancellationToken = token,
                MaxDegreeOfParallelism = _maxConcurrentWorkers,
                TaskScheduler = TaskScheduler.Default
            };
        }

        private static bool FindPropsFileRootDirectory(string defaultConfigPath, out string propsFileRootDirectory)
        {
            propsFileRootDirectory = string.Empty;

            FileInfo defaultConfigInfo;
            if (string.IsNullOrWhiteSpace(defaultConfigPath) || (defaultConfigInfo = new FileInfo(defaultConfigPath)).Exists == false)
            {
                Trace.WriteLine($"Cannot deduce root search directory for .props files: default.config was not found in path \"{defaultConfigPath}\"");
                return false;
            }

            // Go up until repo root folder from .corext/Config/default.config
            DirectoryInfo? vsRoot = defaultConfigInfo.Directory?.Parent?.Parent;

            if (vsRoot == null)
            {
                Trace.WriteLine("Failed to deduce root search directory for .props files: given default.config is not in a VS repo.");
                propsFileRootDirectory = string.Empty;
                return false;
            }

            // Go down to src/SetupPackages from src folder
            propsFileRootDirectory = Path.Combine(vsRoot.FullName, "src", "SetupPackages");

            if (!Directory.Exists(propsFileRootDirectory))
            {
                Trace.WriteLine("Failed to deduce root search directory for .props files: given default.config is not in a VS repo.");
                propsFileRootDirectory = string.Empty;
                return false;
            }

            return true;
        }
    }
}