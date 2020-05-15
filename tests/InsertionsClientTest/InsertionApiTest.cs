// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Api;
using Microsoft.Net.Insertions.Api.Providers;
using Microsoft.Net.Insertions.Common.Constants;
using Microsoft.Net.Insertions.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InsertionsClientTest
{
    [TestClass]
    public class InsertionApiTest
    {
        [TestMethod]
        [DataRow(IgnoreCase.None)]
        [DataRow(IgnoreCase.DefaultDevUxTeamPackages)]
        [DataRow(IgnoreCase.SpecifiedFile)]
        public void TestLoadFile(IgnoreCase ignoreCase)
        {
            IInsertionApiFactory apiFactory = new InsertionApiFactory();
            IInsertionApi api = apiFactory.Create(TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(4));

            UpdateResults results;
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string manifestFile = Path.Combine(assetsDirectory, "manifest.json");
            string defaultConfigFile = Path.Combine(assetsDirectory, "default.config");

            results = ignoreCase switch
            {
                IgnoreCase.DefaultDevUxTeamPackages => api.UpdateVersions(manifestFile, defaultConfigFile, InsertionConstants.DefaultDevUxTeamPackages, null, null),
                IgnoreCase.SpecifiedFile => api.UpdateVersions(manifestFile, defaultConfigFile, Path.Combine(assetsDirectory, "ignored.txt"), null, null),
                _ => api.UpdateVersions(manifestFile, defaultConfigFile, (HashSet<string>?)null, null, null),
            };

            Assert.IsTrue(ListsAreEquivalent(ignoreCase, results?.IgnoredNuGets), $"Mismatched ignore packages for {ignoreCase}");
        }

        private bool ListsAreEquivalent(IgnoreCase ignoreCase, HashSet<string>? results)
        {
            HashSet<string>? expected = ignoreCase switch
            {
                IgnoreCase.DefaultDevUxTeamPackages => InsertionConstants.DefaultDevUxTeamPackages,
                IgnoreCase.SpecifiedFile =>
                new HashSet<string>(File.ReadAllLines(Path.Combine(Directory.GetCurrentDirectory(), "Assets", "ignored.txt"))),
                _ => null,
            };

            if (expected == null)
            {
                return results == null;
            }

            return results == null ? false : !expected.Except(results).Any();
        }
    }
}