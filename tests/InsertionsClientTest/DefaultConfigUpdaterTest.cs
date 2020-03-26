// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Api;
using Microsoft.Net.Insertions.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace InsertionsClientTest
{
	[TestClass]
	public class DefaultConfigUpdaterTest
	{
		/// <summary>
		/// Tests the behaviour when config file path is null or points to a nonexistent file.
		/// </summary>
		/// <param name="configFilePath">Path to default.config</param>
		[TestMethod]
		[DataRow(null, DisplayName = "Null config path")]
		[DataRow("some nonexistent file.txt", DisplayName = "Nonexistent config file")]
		public void TestLoadWrongFile(string configFilePath)
		{
			DefaultConfigUpdater updater = new DefaultConfigUpdater();
			Assert.IsFalse(updater.TryLoad(configFilePath, out string error));
			Assert.IsFalse(string.IsNullOrWhiteSpace(error));
		}

		/// <summary>
		/// Attempts to load a sample default.config file and related .packageconfig files
		/// </summary>
		[TestMethod]
		public void TestLoadFile()
		{
			CreateAndLoadDefaultConfigUpdater();
		}

		/// <summary>
		/// Attempts to change only 1 of the package versions, then saves the results.
		/// Checks if one and only one file was modified.
		/// Checks if modification was successful.
		/// 2 package ids are tested: one lies within default.config, the other lies within a packageconfig
		/// </summary>
		/// <param name="packageId">Id of the package to change the version of</param>
		[TestMethod]
		[DataRow("VS.Tools.Roslyn", DisplayName = "Update default.config content")]
		[DataRow("Microsoft.IdentityModel.Clients.ActiveDirectory", DisplayName = "Update packageconfig content")]
		public void TestDefaultConfigContent(string packageId)
		{
			// Load file
			DefaultConfigUpdater updater = CreateAndLoadDefaultConfigUpdater();

			// Test Content: change a version in default.config or packageconfig, depending on the input
			Assert.IsTrue(updater.TryUpdatePackage(packageId, "1.2.3.4.5", out _));
			FileSaveResult[] saveResults = updater.Save();
			Assert.IsNotNull(saveResults);
			Assert.AreEqual(1, saveResults.Length);

			Assert.IsTrue(saveResults[0].Succeeded);
		}

		/// <summary>
		/// Attempts to update the version of a package that doesn't exist in config files.
		/// Checks that no files were modified as a result of this operation.
		/// </summary>
		[TestMethod]
		public void TestMissingPackageUpdate()
		{
			// Load file
			DefaultConfigUpdater updater = CreateAndLoadDefaultConfigUpdater();

			// Update a package that doesn't exist
			Assert.IsFalse(updater.TryUpdatePackage("Some.Package.Nobody.Created", "xxx", out _));
			FileSaveResult[] saveResults = updater.Save();
			Assert.IsNotNull(saveResults);
			Assert.AreEqual(0, saveResults.Length);
		}

		/// <summary>
		/// Changes the version number of a package to a GUID. Opens and reads the saved file to check
		/// if the GUID really exists in it or not.
		/// 2 package ids are tested: one lies within default.config, the other lies within a packageconfig
		/// </summary>
		/// <param name="packageId">Id of the package to change the version of</param>
		[TestMethod]
		[DataRow("VS.Redist.X86.Retail.Bin.I386.HelpDocs.Intellisense.NETPortableV4_0.1055", DisplayName = "Version number set: default.config")]
		[DataRow("Microsoft.VisualStudio.Language.NavigateTo.Implementation", DisplayName = "Version number set: packageconfig")]
		public void TestSetCorrectVersionDefaultConfig(string packageId)
		{
			// Load file
			DefaultConfigUpdater updater = CreateAndLoadDefaultConfigUpdater();

			// Test Content
			string versionNumber = Guid.NewGuid().ToString();
			// Edit a package that is inside default.config or .packageconfig depending on the input
			Assert.IsTrue(updater.TryUpdatePackage(packageId, versionNumber, out _));
			FileSaveResult[] saveResults = updater.Save();
			Assert.IsNotNull(saveResults);
			Assert.AreEqual(1, saveResults.Length);

			Assert.IsTrue(saveResults[0].Succeeded);
			Assert.IsTrue(File.ReadAllText(saveResults[0].Path).Contains(versionNumber), "Written version number was not found in the saved file.");
		}

		private DefaultConfigUpdater CreateAndLoadDefaultConfigUpdater()
		{
			DefaultConfigUpdater updater = new DefaultConfigUpdater();
			string defaultConfigPath = Path.Combine(Environment.CurrentDirectory, "Assets", "default.config");
			Assert.IsTrue(File.Exists(defaultConfigPath));
			Assert.IsTrue(updater.TryLoad(defaultConfigPath, out string error), error);
			Assert.IsTrue(string.IsNullOrEmpty(error));

			return updater;
		}
	}
}
