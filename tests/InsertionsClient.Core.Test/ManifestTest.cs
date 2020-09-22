// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Api.Providers;
using Microsoft.DotNet.InsertionsClient.Common.Json;
using Microsoft.DotNet.InsertionsClient.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DefaultConfigClientTest
{
    [TestClass]
    public class ManifestTest
    {
        /// <summary>
        /// Test existence and format of manifest.json file that will be used in other test cases
        /// </summary>
        [TestMethod]
        public void TestIfValidJson()
        {
            string manifestFile = GetManifestFilePath();
            Assert.IsTrue(File.Exists(manifestFile));
            Manifest instance = Serializer.Deserialize<Manifest>(File.ReadAllText(manifestFile));
            Assert.IsNotNull(instance);
            string json = Serializer.Serialize(instance);
            Assert.IsFalse(string.IsNullOrWhiteSpace(json));
        }

        /// <summary>
        /// Tests the behaviour when manifest file path is wrong.
        /// </summary>
        /// <param name="manifestPath">Path to manifest.json file</param>
        [TestMethod]
        [DataRow(null, DisplayName = "Validate null manifest path")]
        [DataRow("some nonexisent file.png", DisplayName = "Validate nonexistent manifest file")]
        public void TestTryValidate(string manifestPath)
        {
            InsertionApi insertionApi = new InsertionApi();
            Assert.IsFalse(insertionApi.TryValidateManifestFile(manifestPath, out string details));
            Assert.IsFalse(string.IsNullOrWhiteSpace(details));
        }

        /// <summary>
        /// Tests the behaviour when content of manifest file is not a valid JSON.
        /// </summary>
        [TestMethod]
        public void TestLoadNonJsonFile()
        {
            InsertionApi insertionApi = new InsertionApi();
            List<Asset> assets = new List<Asset>();
            
            string fakeManifestPath = Path.Combine(Environment.CurrentDirectory, "fakeManifest.json");
            File.WriteAllText(fakeManifestPath, "some content that is not json");

            bool result = insertionApi.TryExtractManifestAssets(fakeManifestPath, assets, out string error);
            Assert.IsFalse(result);
            Assert.IsFalse(string.IsNullOrWhiteSpace(error));
            Assert.IsNotNull(assets);
            Assert.AreEqual(0, assets.Count);
        }

        /// <summary>
        /// Attempts to read and load the contents of a sample manifest file.
        /// </summary>
        [TestMethod]
        public void TestLoad()
        {
            LoadManifestAssets();
        }

        /// <summary>
        /// Tests if names of the assets from sample manifest file were loaded correctly
        /// </summary>
        [TestMethod]
        public void TestLoadedContent()
        {
            List<Asset> assets = LoadManifestAssets();

            Assert.IsTrue(assets.Any(a => a.Name == "Microsoft.NETCore.Jit"));
            Assert.IsTrue(assets.All(a => a.Name != null), "Asset name was not loaded correctly and is null");
        }

        private string GetManifestFilePath()
        {
            return Path.Combine(Environment.CurrentDirectory, "Assets", "manifest.json");
        }

        private List<Asset> LoadManifestAssets()
        {
            InsertionApi insertionApi = new InsertionApi();
            List<Asset> assets = new List<Asset>();
            bool result = insertionApi.TryExtractManifestAssets(GetManifestFilePath(), assets, out string error);
            Assert.IsTrue(result, error);
            Assert.IsTrue(string.IsNullOrWhiteSpace(error), error);
            Assert.IsNotNull(assets);
            Assert.AreNotEqual(assets.Count, 0);

            return assets;
        }
    }
}