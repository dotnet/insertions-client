// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Models;
using Microsoft.Net.Insertions.Models.Extensions;
using Microsoft.Net.Insertions.Common.Constants;
using Microsoft.Net.Insertions.Common.Json;
using Microsoft.Net.Insertions.Telemetry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Text;

namespace Microsoft.Net.Insertions.Api.Providers
{
    /// <summary>
    /// <see cref="IInsertionApi"/> provider relying on <see cref="IDefaultConfigApi"/> instances to update NuGet versions.
    /// </summary>
    internal sealed class InsertionApi : IInsertionApi
    {
        private readonly MeasurementsSession _metrics;
        
        private readonly int _maxWaitSeconds = 75, _maxConcurrentWorkers = 15;

        
        internal InsertionApi(int? maxWaitSeconds = null, int? maxConcurrency = null)
        {
            _metrics = new MeasurementsSession();
            _maxWaitSeconds = Math.Clamp(maxWaitSeconds ?? 120, 60, 120);
            _maxConcurrentWorkers = Math.Clamp(maxConcurrency ?? 20, 1, 20);
        }


        #region IInsertionApi API
        public UpdateResults UpdateVersions(string manifestFile, string defaultConfigFile)
        {
            return UpdateVersions(manifestFile, defaultConfigFile, default(HashSet<string>));
        }

        public UpdateResults UpdateVersions(string manifestFile, string defaultConfigFile, string ignoredPackagesFile)
        {
            return UpdateVersions(manifestFile, defaultConfigFile, LoadPackagesToIgnore(ignoredPackagesFile));
        }

        public UpdateResults UpdateVersions(string manifestFile, string defaultConfigFile, HashSet<string> packagesToIgnore)
        {
            List<Asset> assets = null;
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
            using CancellationTokenSource source = new CancellationTokenSource(TimeSpan.FromSeconds(_maxWaitSeconds));
            try
            {
                _ = Parallel.ForEach(assets,
                    CreateParallelOptions(source.Token),
                    asset => ParallelCallback(asset, packagesToIgnore, configUpdater, results));

                results.FileSaveResults = configUpdater.Save();
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
            Trace.WriteLine("Statistics:");
            foreach(Update update in Enum.GetValues(typeof(Update)))
            {
                Trace.WriteLine($"{update} - {update.GetString()}{Environment.NewLine}{_metrics[update]}");
            }
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
                Manifest buildManifest = DeserializeManifest(manifestFile);
                if (!buildManifest.Validate())
                {
                    assets = new List<Asset>();
                    details = $"Validation of de-serialized {InsertionConstants.ManifestFile} file content failed.";
                    return false;
                }
                Trace.WriteLine($"De-serialized {buildManifest.Builds.Count} builds from {InsertionConstants.ManifestFile}.");

                ConcurrentDictionary<string, Asset> map = new ConcurrentDictionary<string, Asset>();
                foreach (Build build in buildManifest.Builds.AsParallel())
                {
                    foreach (Asset asset in build.Assets.AsParallel())
                    {
                        if (!map.TryAdd(asset.Name, asset))
                        {
                            if (map.ContainsKey(asset.Name))
                            {
                                Trace.WriteLine($"Duplicate entry in the specified {InsertionConstants.ManifestFile} for asset {asset.Name}.");
                            }
                            else
                            {
                                Trace.WriteLine($"Problem processing {asset.Name} in the specified {InsertionConstants.ManifestFile}.");
                            }
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

        private Manifest DeserializeManifest(string manifestFile)
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

        private HashSet<string> LoadPackagesToIgnore(string ignoredPackagesFile)
        {
            if (!File.Exists(ignoredPackagesFile))
            {
                return new HashSet<string>();
            }

            HashSet<string> ignoredPackages = new HashSet<string>();
            string[] fileLines = File.ReadAllLines(ignoredPackagesFile);

            foreach(string line in fileLines)
            {
                ignoredPackages.Add(line);
            }

            return ignoredPackages;
        }

        private void ParallelCallback(Asset asset, HashSet<string> packagesToIgnore, DefaultConfigUpdater configUpdater, UpdateResults results)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();

            if (!TryGetPackageId(asset.Name, asset.Version, out string packageId))
            {
                _metrics.AddMeasurement(Update.NotAPackage, stopWatch.ElapsedTicks);
                return;
            }

            if (packagesToIgnore != null && packagesToIgnore.Contains(packageId))
            {
                _metrics.AddMeasurement(Update.Ignored, stopWatch.ElapsedTicks);
                Trace.WriteLine($"Skipping {packageId} since it was requested to be ignored.");
                return;
            }

            if (!configUpdater.TryUpdatePackage(packageId, asset.Version, out string oldVersion))
            {
                _metrics.AddMeasurement(Update.NoMatch, stopWatch.ElapsedTicks);
                return;
            }

            _metrics.AddMeasurement(Update.ExactMatch, stopWatch.ElapsedTicks);

            if(oldVersion != asset.Version)
            {
                results.AddPackage(packageId, asset.Version);
                Trace.WriteLine($"Package {packageId} was updated to version {asset.Version}");
            }
        }

        private bool TryGetPackageId(string assetName, string version, out string packageId)
        {
            packageId = null;

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
            while(index < filename.Length)
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
    }
}