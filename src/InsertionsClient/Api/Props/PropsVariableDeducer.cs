// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Api.Props.Models;
using Microsoft.Net.Insertions.Models;
using Microsoft.Net.Insertions.Props.Models;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Microsoft.Net.Insertions.Api
{
    /// <summary>
    /// Deduces the values of given props file variables by matching:
    /// <list type="bullet">
    /// <item> Paths to files extracted from nuget packages into the folders specified in default.config and .packageconfig files </item>
    /// <item> Paths to files specified in swr files. </item>
    /// </list>
    /// </summary>
    internal sealed class PropsVariableDeducer
    {
        /// <summary>
        /// Nuget feed used to download packages
        /// </summary>
        private readonly string _feed;

        /// <summary>
        /// Credentials for the feed.
        /// Null, if no access token was provided.
        /// </summary>
        private readonly PackageSourceCredential? _credentials;

        /// <summary>
        /// Creates an instance of <see cref="PropsVariableDeducer"/>
        /// </summary>
        /// <param name="feed">Url to the feed that will be used to download packages</param>
        /// <param name="accessToken">Token used to access private feed to download packages</param>
        public PropsVariableDeducer(string feed, string? accessToken)
        {
            _feed = feed;

            if (accessToken != null)
            {
                _credentials = new PackageSourceCredential(feed, accessToken, accessToken, true, null);
            }
        }

        /// <summary>
        /// Deduces the value of the variables in swr files
        /// </summary>
        /// <param name="defaultConfigUpdater"><see cref="DefaultConfigUpdater"/> that will provide extract locations for packages.</param>
        /// <param name="packages">Nuget packages that should be downloaded and used.</param>
        /// <param name="swrFiles">Files, containing the variables.</param>
        /// <param name="deducedVariablesList">List of variables, value of which was found.</param>
        /// <param name="outcomeDetails">Detail string, explaining the outcome of the operation.</param>
        /// <param name="maximumWaitSeconds">Maximum duration to wait for nuget downloads to complete.</param>
        /// <returns>True if operation succeeded. False otherwise</returns>
        public bool DeduceVariableValues(DefaultConfigUpdater defaultConfigUpdater, IEnumerable<PackageUpdateResult> packages,
            SwrFile[] swrFiles, out List<PropsFileVariableReference> deducedVariablesList, out string outcomeDetails, int maximumWaitSeconds = -1)
        {
            using CancellationTokenSource tokenSource = new CancellationTokenSource();
            ConcurrentBag<PropsFileVariableReference> deducedVariables = new ConcurrentBag<PropsFileVariableReference>();

            TransformManyBlock<PackageUpdateResult, string> nugetDownloadBlock = new TransformManyBlock<PackageUpdateResult, string>(async (packageUpdate) =>
            {
                return await GetPackageFileListAsync(defaultConfigUpdater, packageUpdate, tokenSource.Token).ConfigureAwait(false);
            });

            ActionBlock<string> filenameMatchBlock = new ActionBlock<string>((filename) =>
            {
                foreach (PropsFileVariableReference varReference in MatchFileNames(swrFiles, filename))
                {
                    deducedVariables.Add(varReference);
                }
            });

            nugetDownloadBlock.LinkTo(filenameMatchBlock, new DataflowLinkOptions() { PropagateCompletion = true });

            foreach (PackageUpdateResult package in packages)
            {
                nugetDownloadBlock.Post(package);
            }

            nugetDownloadBlock.Complete();
            bool executedToCompletion = filenameMatchBlock.Completion.Wait(maximumWaitSeconds == -1 ? -1 : (maximumWaitSeconds * 1000));

            if(executedToCompletion == false)
            {
                outcomeDetails = "Operation timed out. Failed to download and process all the nuget packages in time.";
                tokenSource.Cancel();
            }
            else
            {
                outcomeDetails = string.Empty;
            }

            deducedVariablesList = deducedVariables.ToList();
            return executedToCompletion;
        }

        private async Task<IEnumerable<string>> GetPackageFileListAsync(DefaultConfigUpdater defaultConfigUpdater, PackageUpdateResult packageUpdate, CancellationToken? cancellationToken = null)
        {
            string? link = defaultConfigUpdater.GetPackageLink(packageUpdate.PackageId);

            if (link == null)
            {
                // We don't know where nuget package should be extracted to.
                return Enumerable.Empty<string>();
            }

            SourceCacheContext cache = new SourceCacheContext();
            NuGetVersion packageVersion = new NuGetVersion(packageUpdate.NewVersion);

            // We have to specify some cancellation token, if caller doesn't provide one
            CancellationTokenSource? ownedCancellationToken = cancellationToken == null ? new CancellationTokenSource() : null;

            // Stream that will receive the package bytes
            using MemoryStream packageStream = new MemoryStream();

            try
            {
                SourceRepository rep = Repository.Factory.GetCoreV3(_feed, FeedType.HttpV3);
                if (_credentials != null)
                {
                    rep.PackageSource.Credentials = _credentials;
                }

                FindPackageByIdResource resource = await rep.GetResourceAsync<FindPackageByIdResource>().ConfigureAwait(false);

                Trace.WriteLine($"Downloading package {packageUpdate.PackageId}-{packageUpdate.NewVersion} into memory.");

                bool downloadResult = await resource.CopyNupkgToStreamAsync(
                    packageUpdate.PackageId,
                    packageVersion,
                    packageStream,
                    cache,
                    NullLogger.Instance,
                    cancellationToken ?? ownedCancellationToken!.Token)
                    .ConfigureAwait(false);

                if (!downloadResult)
                {
                    Trace.WriteLine($"There is an issue downloading nuget package: {packageUpdate.PackageId}");
                    return Enumerable.Empty<string>();
                }

                Trace.WriteLine($"Downloading complete for package {packageUpdate.PackageId}.");
                return GetNugetFileList(packageStream, link);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"There is an issue downloading nuget package.");
                Trace.WriteLine(e.ToString());
                return Enumerable.Empty<string>();
            }
            finally
            {
                ownedCancellationToken?.Dispose();
            }
        }

        private IEnumerable<PropsFileVariableReference> MatchFileNames(SwrFile[] swrFiles, string filename)
        {
            foreach (SwrFile swrFile in swrFiles)
            {
                foreach (PayloadPath payloadPath in swrFile.PayloadPaths)
                {
                    Match match = payloadPath.Pattern.Match(filename);
                    if (!match.Success)
                    {
                        continue;
                    }

                    yield return new PropsFileVariableReference(payloadPath.VariableName, match.Groups[1].Value, swrFile.Path);
                }
            }
        }
    
        /// <summary>
        /// Retrieves the paths to all the files in the nuget package as if they were extracted to the location
        /// specified in <see cref="directory"/> parameter.
        /// </summary>
        /// <param name="packageDataStream">Stream containing the nuget package binary.</param>
        /// <param name="directory">Directory for the extracted files in the package.</param>
        /// <returns></returns>
        private List<string> GetNugetFileList(Stream packageDataStream, string directory)
        {
            using PackageArchiveReader packageReader = new PackageArchiveReader(packageDataStream);

            return packageReader.GetFiles().Select(file =>
            {
                // Construct path to the file
                // Link always has backslash. Make sure it is consistent.
                return directory + "\\" + file.Replace('/', '\\');
            }).ToList();
        }
    }
}
