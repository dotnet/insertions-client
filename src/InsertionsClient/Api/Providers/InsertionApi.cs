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
        private const string ElementNamePackage = "package";

        private readonly MeasurementsSession _metrics;

        private readonly int _maxWaitSeconds = 75, _maxConcurrentWorkers = 15;

        private XDocument _xmlDoc = null;


        internal InsertionApi(string maxWaitSeconds = null, string maxConcurrency = null)
        {
            _metrics = new MeasurementsSession();
            _maxWaitSeconds = ParseInt(maxWaitSeconds, "max wait seconds", 60, 120);
            _maxConcurrentWorkers = ParseInt(maxConcurrency, "max concurrency", 1, 20);
        }


        #region IInsertionApi API
        public UpdateResults UpdateVersions(string manifestFile, string defaultConfigFile)
        {
            IEnumerable<Asset> assets = Enumerable.Empty<Asset>();

            if (!TryValidateManifestFile(manifestFile, out string details)
                || !TryExtractManifestAssets(manifestFile, out assets, out details)
                || !TryLoadDefaultConfig(defaultConfigFile, out details))
            {
                new UpdateResults { OutcomeDetails = details };
            }

            UpdateResults results = new UpdateResults();
            Stopwatch overallRunStopWatch = Stopwatch.StartNew();
            using CancellationTokenSource source = new CancellationTokenSource(TimeSpan.FromSeconds(_maxWaitSeconds));
            try
            {
                IEnumerable<XElement> packageXElements = _xmlDoc.Descendants(ElementNamePackage);
                if (packageXElements == null || !packageXElements.Any())
                {
                    return new UpdateResults { OutcomeDetails = $"{ElementNamePackage} element does not exist" };
                }

                _ = Parallel.ForEach(packageXElements,
                    CreateParallelOptions(source.Token),
                    packageXElement => ParallelCallback(packageXElement, assets, results));

                _xmlDoc.Save(defaultConfigFile);
            }
            catch (Exception e)
            {
                results.OutcomeDetails = e.Message;
            }
            finally
            {
                results.DurationMilliseconds = overallRunStopWatch.ElapsedMilliseconds;
                foreach(string item in Enum.GetNames(typeof(Update)))
                {
                    LogStatistics(Enum.Parse<Update>(item));
                }
            }
            return results;
        }
        #endregion


        private static int ParseInt(string boxedInt, string name, int minValue, int maxValue)
        {
            int targetValue = 0;
            try
            {
                if (string.IsNullOrWhiteSpace(boxedInt))
                {
                    targetValue = maxValue;
                    return targetValue;
                }
                targetValue = int.TryParse(boxedInt, out int tmp) ? tmp : maxValue;

                if (targetValue > maxValue)
                {
                    targetValue = maxValue;
                }
                if (targetValue < minValue)
                {
                    targetValue = minValue;
                }
                return targetValue;
            }
            finally
            {
                Trace.WriteLine($"Set \"{name}\" to {targetValue}");
            }
        }

        private void LogStatistics(Update update)
        {
            Trace.WriteLine($"Statistics: {update} - {update.GetString()}{Environment.NewLine}{_metrics[update].ToString()}");
        }

        private bool TryValidateManifestFile(string manifestFile, out string details)
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

        private bool TryExtractManifestAssets(string manifestFile, out IEnumerable<Asset> assets, out string details)
        {
            assets = Enumerable.Empty<Asset>();
            details = string.Empty;

            try
            {
                Manifest buildManifest = DeserializeManifest(manifestFile);
                if (!buildManifest.Validate())
                {
                    details = $"Validation of de-serialized {InsertionConstants.ManifestFile} file content failed.";
                    return false;
                }
                Trace.WriteLine($"De-serialized {buildManifest.Builds.Count} builds from {InsertionConstants.ManifestFile}.");

                ConcurrentDictionary<string, Asset> map = new ConcurrentDictionary<string, Asset>();
                foreach (Build build in buildManifest.Builds.AsParallel())
                {
                    foreach (var asset in build.Assets.AsParallel())
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
                if (map.Count < 1)
                {
                    details = $"No assets in {InsertionConstants.ManifestFile}";
                }
                else
                {
                    assets = map.Values.OrderBy(x => x.Name);
                }
            }
            catch (Exception e)
            {
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

        private bool TryLoadDefaultConfig(string defaultConfigPath, out string details)
        {
            details = string.Empty;

            _xmlDoc = null;

            if (string.IsNullOrWhiteSpace(defaultConfigPath))
            {
                details = $"{InsertionConstants.DefaultConfigFile} cannot be null";
            }
            else if (!File.Exists(defaultConfigPath))
            {
                details = $"Inexistent file {defaultConfigPath}";
            }
            else
            {
                Trace.WriteLine($"Loading {InsertionConstants.DefaultConfigFile} content from {defaultConfigPath}.");
                _xmlDoc = XDocument.Load(defaultConfigPath);
                Trace.WriteLine($"Loaded {InsertionConstants.DefaultConfigFile} content.");
            }

            return string.IsNullOrWhiteSpace(details);
        }

        private void ParallelCallback(XElement packageXElement, IEnumerable<Asset> assets, UpdateResults results)
        {
            Stopwatch stopWatch = Stopwatch.StartNew();
            string packageId = packageXElement.Attribute("id").Value;
            IEnumerable<Asset> matches = assets.Where(x => x.Name.Contains(packageId));
            if (matches.Any())
            {
                XAttribute versionAttribute = packageXElement.Attribute("version");
                string version = string.Empty;
                Update update = Update.FailedUpdate;

                if (matches.Select(x => x.Version).Distinct().Count() == 1)
                {
                    version = matches.First().Version;
                    update = Update.CommonVersion;
                }
                else
                {
                    IEnumerable<Asset> matchingAsset = assets.Where(x => x.Name == packageId);
                    if (matchingAsset.Any())
                    {
                        version = matchingAsset.First().Version;
                        update = Update.ExactMatch;
                    }
                }

                if (versionAttribute == null)
                {
                    Trace.WriteLine($"Package id {packageId} lacked \"version\" attribute.");
                    update = Update.FailedUpdate;
                }

                _metrics.AddMeasurement(update, stopWatch.ElapsedMilliseconds);
                if (update == Update.FailedUpdate)
                {
                    Trace.WriteLine($"{GetNugetMatchDetails(packageId, matches)}{packageId} version NOT set.{Environment.NewLine}");
                }
                else
                {
                    versionAttribute.Value = version;
                    Trace.WriteLine($"{GetNugetMatchDetails(packageId, matches)}Set {packageId} version to {version}{Environment.NewLine}Update type: {update}.{Environment.NewLine}");
                    results.AddPackage(packageId, version);
                }
            }
            else
            {
                _metrics.AddMeasurement(Update.NoMatch, stopWatch.ElapsedMilliseconds);
            }
        }
        
        private string GetNugetMatchDetails(string packageId, IEnumerable<Asset> assets)
        {
            StringBuilder txt = new StringBuilder();
            _ = txt.AppendLine($"Found match(es) for package {packageId}. Asset {assets.Count()}-match(es): ");
            foreach (var asset in assets)
            {
                _ = txt.AppendLine(asset.ToString());
            }
            return txt.ToString();
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