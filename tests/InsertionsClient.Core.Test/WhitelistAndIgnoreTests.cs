using Microsoft.DotNet.InsertionsClient.Api;
using Microsoft.DotNet.InsertionsClient.Api.Providers;
using Microsoft.DotNet.InsertionsClient.Common.Constants;
using Microsoft.DotNet.InsertionsClient.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace InsertionsClient.Core.Test
{
    [TestClass]
    public class WhitelistAndIgnoreTests
    {
        [TestInitialize]
        public void Initialize()
        {
            // Everytime we run the api, default config is modified and saved. Keep a copy of the original default.config.
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string defaultConfigFile = Path.Combine(assetsDirectory, "default.config");
            File.Copy(defaultConfigFile, defaultConfigFile + ".copy", true);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Restore default.config from the backup we created
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string defaultConfigFile = Path.Combine(assetsDirectory, "default.config");
            File.Move(defaultConfigFile + ".copy", defaultConfigFile, true);
        }

        [TestMethod]
        public void TestEmptyIgnorelist()
        {
            IInsertionApiFactory apiFactory = new InsertionApiFactory();
            IInsertionApi api = apiFactory.Create(TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(4));

            UpdateResults results;
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string manifestFile = Path.Combine(assetsDirectory, "manifest.json");
            string defaultConfigFile = Path.Combine(assetsDirectory, "default.config");
            IEnumerable<Regex> whitelistedPackages = Enumerable.Empty<Regex>();
            ImmutableHashSet<string> ignoredPackages = ImmutableHashSet<string>.Empty;

            results = api.UpdateVersions(
                    manifestFile,
                    defaultConfigFile,
                    whitelistedPackages,
                    ignoredPackages,
                    null,
                    null);

            Assert.IsFalse(results.IgnoredNuGets.Any(), "No packages should have been ignored.");
        }

        [TestMethod]
        public void TestIgnorelist()
        {
            IInsertionApiFactory apiFactory = new InsertionApiFactory();
            IInsertionApi api = apiFactory.Create(TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(4));

            UpdateResults results;
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string manifestFile = Path.Combine(assetsDirectory, "manifest.json");
            string defaultConfigFile = Path.Combine(assetsDirectory, "default.config");
            IEnumerable<Regex> whitelistedPackages = Enumerable.Empty<Regex>();

            ImmutableHashSet<string> ignoredPackages = ImmutableHashSet.Create(new string[]{
                @"^VS\.Redist\.Common\.NetCore\.AppHostPack\.x86_x64\.3\.1",
                @"^VS\.Redist\.Common\.NetCore\.SharedFramework\.(x86|x64)\.[0-9]+\.[0-9]+$"
            });

            results = api.UpdateVersions(
                    manifestFile,
                    defaultConfigFile,
                    whitelistedPackages,
                    ignoredPackages,
                    null,
                    null);

            // Ignore all modified files except for one
            Assert.IsFalse(results.UpdatedNuGets.Any(n => ignoredPackages.Contains(n.PackageId)), "A package that should have been ignored was updated.");
            Assert.IsFalse(results.IgnoredNuGets.Any(n => !ignoredPackages.Contains(n)), "A package was ignored, but it shouldn't have been");
        }

        [TestMethod]
        public void TestEmptyWhitelist()
        {
            IInsertionApiFactory apiFactory = new InsertionApiFactory();
            IInsertionApi api = apiFactory.Create(TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(4));

            UpdateResults results;
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string manifestFile = Path.Combine(assetsDirectory, "manifest.json");
            string defaultConfigFile = Path.Combine(assetsDirectory, "default.config");
            IEnumerable<Regex> whitelistedPackages = Enumerable.Empty<Regex>();
            ImmutableHashSet<string> ignoredPackages = ImmutableHashSet<string>.Empty;

            results = api.UpdateVersions(
                    manifestFile,
                    defaultConfigFile,
                    whitelistedPackages,
                    ignoredPackages,
                    null,
                    null);

            Assert.IsTrue(results.UpdatedNuGets.Any(), "Empty whitelist shouldn't prevent package updates, but no packages were updated.");
        }

        [TestMethod]
        public void TestWhitelist()
        {
            IInsertionApiFactory apiFactory = new InsertionApiFactory();
            IInsertionApi api = apiFactory.Create(TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(4));

            UpdateResults results;
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string manifestFile = Path.Combine(assetsDirectory, "manifest.json");
            string defaultConfigFile = Path.Combine(assetsDirectory, "default.config");
            ImmutableHashSet<string> ignoredPackages = ImmutableHashSet<string>.Empty;

            IEnumerable<Regex> whitelistedPackages = new Regex[]
            {
                new Regex(@"VS\.Redist\.Common\.NetCore\.AppHostPack\.x86_x64\.3\.1"),
                new Regex(@"^VS\.Redist\.Common\.NetCore\.SharedFramework\.(x86|x64)\.[0-9]+\.[0-9]+$")
            };

            results = api.UpdateVersions(
                    manifestFile,
                    defaultConfigFile,
                    whitelistedPackages,
                    ignoredPackages,
                    null,
                    null);

            Assert.IsFalse(results.UpdatedNuGets.Any(n => whitelistedPackages.All(pattern => !pattern.IsMatch(n.PackageId))),
                "A package was updated even though it wasn't in the whitelist.");
        }
    }
}
