// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Models;
using Microsoft.Net.Insertions.Common.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace DefaultConfigClientTest
{
    [TestClass]
    public class ManifestTest
    {
        [TestMethod]
        public void TestNominativeManifestSerialization()
        {
            string manifestFile = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "manifest.json");
            Assert.IsTrue(File.Exists(manifestFile));
            var instance = Serializer.Deserialize<Manifest>(File.ReadAllText(manifestFile));
            Assert.IsNotNull(instance);
            string json = Serializer.Serialize(instance);
            Assert.IsFalse(string.IsNullOrWhiteSpace(json));
        }
    }
}