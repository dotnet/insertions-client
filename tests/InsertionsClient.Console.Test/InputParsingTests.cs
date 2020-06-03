// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Api;
using Microsoft.DotNet.InsertionsClient.Api.Providers;
using Microsoft.DotNet.InsertionsClient.ConsoleApp;
using Microsoft.DotNet.InsertionsClient.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace InsertionsClient.Console.Test
{
    [TestClass]
    public class InputParsingTests
    {
        [TestMethod]
        public void TestWhitelistLoading()
        {
            string whitelistFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "whitelist.txt");
            IEnumerable<Regex> whitelistedPackages = InputLoading.LoadWhitelistedPackages(whitelistFilePath);

            Assert.IsTrue(whitelistedPackages.Any(pattern => pattern.IsMatch("VS.Redist.Common.NetCore.Toolset.x64")));
            Assert.IsTrue(whitelistedPackages.Any(pattern => pattern.IsMatch("VS.Redist.Common.NetCore.AppHostPack.x86_x64.3.1")));
            Assert.IsFalse(whitelistedPackages.Any(pattern => pattern.IsMatch("some.package.that.doesnt.exist")));
        }

        [TestMethod]
        public void TestIgnoreFileLoading()
        {
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            Assert.IsTrue(File.Exists(Path.Combine(assetsDirectory, "ignored.txt")), "Required test file \"ignored.txt\" is missing");

            ImmutableHashSet<string> ignoredPackages = InputLoading.LoadPackagesToIgnore(Path.Combine(assetsDirectory, "ignored.txt"));
            Assert.IsTrue(ignoredPackages.Contains("VS.ExternalAPIs.MSBuild"), "Ignored packages is missing an package.");
            Assert.IsFalse(ignoredPackages.Contains("Microsoft.NETCore.Runtime.CoreCLR"), "Ignored packages has a package it shouldn't have.");
        }

        [TestMethod]
        public void TestLoadedWhitelist()
        {
            IInsertionApiFactory apiFactory = new InsertionApiFactory();
            IInsertionApi api = apiFactory.Create(TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(4));

            UpdateResults results;
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string manifestFile = Path.Combine(assetsDirectory, "manifest.json");
            string defaultConfigFile = Path.Combine(assetsDirectory, "default.config");
            ImmutableHashSet<string> ignoredPackages = ImmutableHashSet<string>.Empty;

            IEnumerable<Regex> whitelistedPackages = InputLoading.LoadWhitelistedPackages(Path.Combine(assetsDirectory, "whitelist.txt"));

            results = api.UpdateVersions(
                    manifestFile,
                    defaultConfigFile,
                    whitelistedPackages,
                    ignoredPackages,
                    null,
                    null);
            
            Assert.IsFalse(results.UpdatedNuGets.Any(n => whitelistedPackages.All(pattern => !pattern.IsMatch(n.PackageId))), "Packages that shouldn't have been updated were updated.");
        }

        [TestMethod]
        public void TestLoadedIgnoredPackages()
        {
            IInsertionApiFactory apiFactory = new InsertionApiFactory();
            IInsertionApi api = apiFactory.Create(TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(4));

            UpdateResults results;
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string manifestFile = Path.Combine(assetsDirectory, "manifest.json");
            string defaultConfigFile = Path.Combine(assetsDirectory, "default.config");
            IEnumerable<Regex> whitelistedPackages = Enumerable.Empty<Regex>();

            ImmutableHashSet<string> ignoredPackages = InputLoading.LoadPackagesToIgnore(Path.Combine(assetsDirectory, "ignored.txt"));

            results = api.UpdateVersions(
                    manifestFile,
                    defaultConfigFile,
                    whitelistedPackages,
                    ignoredPackages,
                    null,
                    null);

            Assert.IsTrue(ignoredPackages.SetEquals(results.IgnoredNuGets), $"Mismatched ignore packages");
            Assert.IsFalse(results.UpdatedNuGets.Any(n => ignoredPackages.Contains(n.PackageId)), "Packages that shouldn't have been updated were updated.");
        }
    }
}
