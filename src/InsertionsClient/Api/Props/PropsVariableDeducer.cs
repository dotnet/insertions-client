// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Api.Models;
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
	internal class PropsVariableDeducer
	{
		/// <summary>
		/// Nuget feed used to download packages
		/// </summary>
		private readonly string _feed;

		/// <summary>
		/// Credentials for the feed.
		/// </summary>
		private readonly PackageSourceCredential _credentials;

		/// <summary>
		/// Creates an instance of <see cref="PropsVariableDeducer"/>
		/// </summary>
		/// <param name="feed">Url to the feed that will be used to download packages</param>
		/// <param name="accessToken">Token used to access private feed to download packages</param>
		public PropsVariableDeducer(string feed, string? accessToken)
		{
			_feed = feed;

			_credentials = new PackageSourceCredential(feed, accessToken, accessToken, true, null);
		}

		/// <summary>
		/// Deduces the value of the variables in swr files
		/// </summary>
		/// <param name="defaultConfigUpdater"></param>
		/// <param name="updatedPackages"></param>
		/// <param name="swrFiles"></param>
		/// <returns></returns>
		public List<PropsFileVariableReference> DeduceVariableValues(DefaultConfigUpdater defaultConfigUpdater, IEnumerable<PackageUpdateResult> updatedPackages, SwrFile[] swrFiles)
		{
			ConcurrentBag<PropsFileVariableReference> deducedVariables = new ConcurrentBag<PropsFileVariableReference>();

			TransformManyBlock<PackageUpdateResult, string> nugetDownloadBlock = new TransformManyBlock<PackageUpdateResult, string>(async (packageUpdate) =>
			{
				return await GetPackageFileList(defaultConfigUpdater, packageUpdate);
			});

			ActionBlock<string> filenameMatchBlock = new ActionBlock<string>((filename) =>
			{
				foreach (PropsFileVariableReference varReference in MatchFileNames(swrFiles, filename))
				{
					deducedVariables.Add(varReference);
				}
			});

			nugetDownloadBlock.LinkTo(filenameMatchBlock, new DataflowLinkOptions() { PropagateCompletion = true });

			foreach (PackageUpdateResult package in updatedPackages)
			{
				nugetDownloadBlock.Post(package);
			}

			nugetDownloadBlock.Complete();
			filenameMatchBlock.Completion.Wait();

			return deducedVariables.ToList();
		}

		private async Task<IEnumerable<string>> GetPackageFileList(DefaultConfigUpdater defaultConfigUpdater, PackageUpdateResult packageUpdate)
		{
			string? link = defaultConfigUpdater.GetPackageLink(packageUpdate.PackageId);

			if (link == null)
			{
				// We don't know where nuget package should be extracted to.
				return Enumerable.Empty<string>();
			}

			SourceCacheContext cache = new SourceCacheContext();
			CancellationTokenSource cancellationToken = new CancellationTokenSource();
			NuGetVersion packageVersion = new NuGetVersion(packageUpdate.NewVersion);

			// Stream that will receive the package bytes
			using MemoryStream packageStream = new MemoryStream();

			try
			{
				SourceRepository? rep = Repository.Factory.GetCoreV3(_feed, FeedType.HttpV3);
				rep.PackageSource.Credentials = _credentials;
				FindPackageByIdResource? resource = await rep.GetResourceAsync<FindPackageByIdResource>();

				Trace.WriteLine($"Downloading package {packageUpdate.PackageId}-{packageUpdate.NewVersion} into memory.");

				bool downloadResult = await resource.CopyNupkgToStreamAsync(
					packageUpdate.PackageId,
					packageVersion,
					packageStream,
					cache,
					NullLogger.Instance,
					cancellationToken.Token);

				if (!downloadResult)
				{
					Trace.WriteLine($"There is an issue downloading nuget package: {packageUpdate.PackageId}");
					return Enumerable.Empty<string>();
				}

				Trace.WriteLine($"Downloading complete for package {packageUpdate.PackageId}.");

				using PackageArchiveReader packageReader = new PackageArchiveReader(packageStream);

				return packageReader.GetFiles().Select(file =>
				{
					// Construct path to the file
					// Link always has backslash. Make sure it is consistent.
					return link + "\\" + file.Replace('/', '\\');
				}).ToList();
			}
			catch (Exception e)
			{
				Trace.WriteLine("There is an issue downloading nuget package: " + e.ToString());
				return Enumerable.Empty<string>();
			}
		}

		private IEnumerable<PropsFileVariableReference> MatchFileNames(SwrFile[] swrFiles, string filename)
		{
			foreach (SwrFile swrFile in swrFiles)
			{
				foreach (SwrFile.PayloadPath payloadPath in swrFile.PayloadPaths)
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
	}
}
