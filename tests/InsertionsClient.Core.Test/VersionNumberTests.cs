using Microsoft.DotNet.InsertionsClient.Api;
using Microsoft.DotNet.InsertionsClient.Api.Providers;
using Microsoft.DotNet.InsertionsClient.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace InsertionsClient.Core.Test
{
    [TestClass]
    public class VersionNumberTests
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
        public void TestVersionNumberComparison()
        {
            IInsertionApiFactory apiFactory = new InsertionApiFactory();
            IInsertionApi api = apiFactory.Create(TimeSpan.FromSeconds(75), TimeSpan.FromSeconds(4));

            UpdateResults results;
            string assetsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string manifestFile = Path.Combine(assetsDirectory, "manifest.json");
            string defaultConfigFile = Path.Combine(assetsDirectory, "default.config");
            IEnumerable<Regex> whitelistedPackages = new Regex[]
            {
                new Regex(@"^VS\.Redist\.Common\.NetCore\.SharedFramework\.(x86|x64)\.[0-9]+\.[0-9]+$")
            };
            ImmutableHashSet<string> ignoredPackages = ImmutableHashSet<string>.Empty;

            results = api.UpdateVersions(
                    manifestFile,
                    defaultConfigFile,
                    whitelistedPackages,
                    ignoredPackages,
                    null,
                    null,
                    null);

            Assert.IsTrue(results.UpdatedPackages.Any(), "Some packages should have been updated.");

            PackageUpdateResult packx64 = results.UpdatedPackages.FirstOrDefault(p => p.PackageId == "VS.Redist.Common.NetCore.SharedFramework.x64.3.1");
            Assert.IsNotNull(packx64);
            Assert.AreEqual(packx64.NewVersion, "4.1.2-servicing.20067.4");

            PackageUpdateResult packx86 = results.UpdatedPackages.FirstOrDefault(p => p.PackageId == "VS.Redist.Common.NetCore.SharedFramework.x86.3.1");
            Assert.IsNotNull(packx86);
            Assert.AreEqual("3.1.3-servicing.20067.4", packx86.NewVersion);
        }
    }
}
