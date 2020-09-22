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

        /// <summary>
        /// Tests whether <see cref="InputLoading.LoadManifestPaths(string, out int)"/> can load a single valid manifest file.
        /// </summary>
        [TestMethod]
        public void TestManifestPathLoading()
        {
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string manifestFile = Path.Combine(assetsDirectory, "manifest.json");
            
            Assert.IsTrue(File.Exists(manifestFile), "Manifest file that is required for the test was not found.");

            List<string> parsedPaths = InputLoading.LoadManifestPaths(manifestFile, out int invalidPathCount);

            Assert.IsNotNull(parsedPaths);
            Assert.AreEqual(1, parsedPaths.Count, "Unexpected number of paths were extracted from the input string.");
            Assert.AreEqual(0, invalidPathCount, "No invalid path was expected.");
            Assert.IsTrue(File.Exists(parsedPaths[0]), $"Parsed path doesn't point to a valid file:{parsedPaths[0]}");

            // Path strings may be different, but they both should point to the same file.
            FileInfo inputFile = new FileInfo(manifestFile);
            FileInfo parsedFile = new FileInfo(parsedPaths[0]);

            Assert.AreEqual(inputFile.FullName, parsedFile.FullName, "Parsed file is not the same file given as input.");
        }

        /// <summary>
        /// Tests whether <see cref="InputLoading.LoadManifestPaths(string, out int)"/> can load manifest files
        /// from an input string that contains a path to a non-existent file.
        /// </summary>
        [TestMethod]
        public void TestInvalidManifestPathLoading()
        {
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string manifestFile = Path.Combine(assetsDirectory, "manifest.json");
            
            Assert.IsTrue(File.Exists(manifestFile), "Manifest file that is required for the test was not found.");
            
            string nonexistentManifestFile = "A file that hopefully doesnt exist.44526223453856905547954332726542";
            string inputString = $"{nonexistentManifestFile};{manifestFile};; ;   ;";

            List<string> parsedPaths = InputLoading.LoadManifestPaths(inputString, out int invalidPathCount);

            Assert.IsNotNull(parsedPaths);
            Assert.AreEqual(1, parsedPaths.Count, "Unexpected number of paths were extracted from the input string.");
            Assert.AreEqual(1, invalidPathCount, "One invalid path was expected.");
            Assert.IsTrue(File.Exists(parsedPaths[0]), $"Parsed path doesn't point to a valid file:{parsedPaths[0]}");

            // Path strings may be different, but they both should point to the same file.
            FileInfo inputFile = new FileInfo(manifestFile);
            FileInfo parsedFile = new FileInfo(parsedPaths[0]);

            Assert.AreEqual(inputFile.FullName, parsedFile.FullName, "Parsed file is not the same file given as input.");
        }

        /// <summary>
        /// Tests whether <see cref="InputLoading.LoadManifestPaths(string, out int)"/> can successfully parse
        /// various input string that contain no paths.
        /// </summary>
        [DataRow(null, DisplayName = "Null input")]
        [DataRow("", DisplayName = "Empty string")]
        [DataRow(";", DisplayName = "Multiple empty strings")]
        [DataRow("  ", DisplayName = "Whitespaces")]
        [DataRow("  ; ;;   ; ", DisplayName = "Multiple whitespaces")]
        [TestMethod]
        public void TestEmptyManifestPathLoading(string inputString)
        {
            List<string> parsedPaths = InputLoading.LoadManifestPaths(inputString, out int invalidPathCount);

            Assert.IsNotNull(parsedPaths);
            Assert.AreEqual(0, parsedPaths.Count, "Unexpected number of paths were extracted from the input string.");
            Assert.AreEqual(0, invalidPathCount, "No invalid path was expected.");
        }

        /// <summary>
        /// Tests whether <see cref="InputLoading.LoadManifestPaths(string, out int)"/> can load manifest files
        /// from an input string that contains a path to a directory instead of a path to a file.
        /// </summary>
        [TestMethod]
        public void TestManifestPathLoadingFolders()
        {
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");

            List<string> parsedPaths = InputLoading.LoadManifestPaths(assetsDirectory, out int invalidPathCount);

            Assert.IsNotNull(parsedPaths);
            Assert.AreEqual(1, parsedPaths.Count, "Unexpected number of paths were extracted from the input string.");
            Assert.AreEqual(0, invalidPathCount, "No invalid path was expected.");

            Assert.IsTrue(File.Exists(parsedPaths[0]), $"Parsed path doesn't point to a valid file:{parsedPaths[0]}");

            DirectoryInfo inputDirectory = new DirectoryInfo(assetsDirectory);
            DirectoryInfo parsedDirectory = new FileInfo(parsedPaths[0]).Directory;

            Assert.AreEqual(inputDirectory.FullName, parsedDirectory.FullName, "Resulting path is not residing under the given folder.");
        }
    }
}
