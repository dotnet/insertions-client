// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Api;
using Microsoft.DotNet.InsertionsClient.Api.Providers;
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
    public sealed class BuildFilterTests
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
        public void TestBuildRejections()
        {
            IInsertionApiFactory apiFactory = new InsertionApiFactory();
            IInsertionApi api = apiFactory.Create(TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(4));

            UpdateResults results;
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string manifestFile = Path.Combine(assetsDirectory, "manifest.json");
            string defaultConfigFile = Path.Combine(assetsDirectory, "default.config");
            ImmutableHashSet<string> ignoredPackages = ImmutableHashSet<string>.Empty;
            IEnumerable<Regex> allowedPackages = new List<Regex>();

            results = api.UpdateVersions(
                    manifestFile,
                    defaultConfigFile,
                    allowedPackages,
                    null,
                    null,
                    null,
                    build => false);

            Assert.IsTrue(results.Outcome, "Insertion should have succeeded, but failed with error: " + results.OutcomeDetails);
            Assert.IsTrue(results.UpdatedNuGets == null || !results.UpdatedNuGets.Any(), "No packages should have been updated.");

            results = api.UpdateVersions(
                    manifestFile,
                    defaultConfigFile,
                    allowedPackages,
                    null,
                    null,
                    null,
                    null);

            Assert.IsTrue(results.Outcome, "Insertion should have succeeded, but failed with error: " + results.OutcomeDetails);
            Assert.IsTrue(results.UpdatedNuGets != null && results.UpdatedNuGets.Any(), "Inconclusive test. This manifest didn't update any packages." +
                " It is not possible to know if build filter worked or not.");
        }
    }
}
