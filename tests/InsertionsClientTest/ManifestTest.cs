// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Models;
using Microsoft.Net.Insertions.Common.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System;
using Microsoft.Net.Insertions.Api.Providers;
using System.Collections.Generic;
using System.Linq;

namespace DefaultConfigClientTest
{
    [TestClass]
    public class ManifestTest
    {
        private string GetManifestFilePath()
        {
            return Path.Combine(Environment.CurrentDirectory, "Assets", "manifest.json");
        }

        [TestMethod]
        public void TestIfValidJson()
        {
            string manifestFile = GetManifestFilePath();
            Assert.IsTrue(File.Exists(manifestFile));
            var instance = Serializer.Deserialize<Manifest>(File.ReadAllText(manifestFile));
            Assert.IsNotNull(instance);
            string json = Serializer.Serialize(instance);
            Assert.IsFalse(string.IsNullOrWhiteSpace(json));
        }

        [TestMethod]
        public void TestTryValidate()
        {
            InsertionApi insertionApi = new InsertionApi();
            Assert.IsFalse(insertionApi.TryValidateManifestFile(null, out string details));
            Assert.IsNotNull(details);
        }

        [TestMethod]
        public void TestTryValidate2()
        {
            InsertionApi insertionApi = new InsertionApi();
            Assert.IsFalse(insertionApi.TryValidateManifestFile("some nonexistant file.png", out string details));
            Assert.IsNotNull(details);
        }

        [TestMethod]
        public void TestLoadNonJsonFile()
        {
            string fakeManifestPath = Path.Combine(Environment.CurrentDirectory, "fakeManifest.json");
            File.WriteAllText(fakeManifestPath, "some content that is not json");
            InsertionApi insertionApi = new InsertionApi();
            bool result = insertionApi.TryExtractManifestAssets(fakeManifestPath, out List<Asset> assets, out string error);
            Assert.IsFalse(result);
            Assert.IsFalse(string.IsNullOrEmpty(error));
            Assert.IsNotNull(assets);
            Assert.AreEqual(assets.Count, 0);
        }

        [TestMethod]
        public void TestLoad()
        {
            InsertionApi insertionApi = new InsertionApi();
            bool result = insertionApi.TryExtractManifestAssets(GetManifestFilePath(), out List<Asset> assets, out string error);
            Assert.IsTrue(result);
            Assert.IsTrue(string.IsNullOrEmpty(error), error);
            Assert.IsNotNull(assets);
            Assert.AreNotEqual(assets.Count, 0);
        }

        [TestMethod]
        public void TestLoadedContent()
        {
            InsertionApi insertionApi = new InsertionApi();
            bool result = insertionApi.TryExtractManifestAssets(GetManifestFilePath(), out List<Asset> assets, out string error);
            Assert.IsTrue(result, error);
            Assert.IsTrue(string.IsNullOrEmpty(error), error);
            Assert.IsNotNull(assets);
            Assert.AreNotEqual(assets.Count, 0);

            Assert.IsTrue(assets.Any(a => a.Name == "Microsoft.NETCore.Jit"));
            Assert.IsTrue(assets.All(a => a.Name != null), "Asset name was not loaded correctly and is null");
        }
    }
}