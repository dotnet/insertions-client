// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Api;
using Microsoft.Net.Insertions.Api.Models;
using Microsoft.Net.Insertions.Api.Providers;
using Microsoft.Net.Insertions.Models;
using Microsoft.Net.Insertions.Props.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InsertionsClientTest
{
	[TestClass]
	public class PropsTests
	{
		/// <summary>
		/// Loads all swr files under test directory and checks if our test .swr file was found
		/// </summary>
		/// <param name="rootPathSuffix">Suffix to add to the end of the search directory path.</param>
		[TestMethod]
		[DataRow("", DisplayName = "Load swr in root directory")]
		[DataRow("../", DisplayName = "Load swr in a child directory")]
		[DataRow("../../", DisplayName = "Load swr in a grandchild directory")]
		public void TestLoadSwr(string rootPathSuffix)
		{
			string swrPath = Path.Combine(Environment.CurrentDirectory, "Assets", "msi.swr");
			SwrFileReader swrFileReader = new SwrFileReader(4);

			string rootSearchPath = Path.Combine(Environment.CurrentDirectory, "Assets", rootPathSuffix);
			rootSearchPath = new DirectoryInfo(rootSearchPath).FullName;

			SwrFile[] loadedSwrFiles = swrFileReader.LoadSwrFiles(rootSearchPath);

			Assert.IsTrue(loadedSwrFiles.Any(s => s.Path == swrPath));
		}

		/// <summary>
		/// Check if test .swr file was loaded successfully
		/// </summary>
		[TestMethod]
		public void TestLoadSwrContent()
		{
			string swrPath = Path.Combine(Environment.CurrentDirectory, "Assets", "msi.swr");
			SwrFileReader swrFileReader = new SwrFileReader(4);

			SwrFile[] loadedSwrFiles = swrFileReader.LoadSwrFiles(Environment.CurrentDirectory);

			SwrFile swrFile = loadedSwrFiles.First(s => s.Path == swrPath);

			Assert.IsNotNull(swrFile.PayloadPaths, "Swr payload list is null");
			Assert.AreEqual(1, swrFile.PayloadPaths.Count, "Loaded swr has wrong number of payload paths");
			Assert.AreEqual("NetCoreAppHostPack31Version", swrFile.PayloadPaths[0].VariableName);
		}

		/// <summary>
		/// Attempts to find a given variable in a props file and update it.
		/// </summary>
		[TestMethod]
		public void TestPropFileUpdate()
		{
			string msiPath = Path.Combine(Environment.CurrentDirectory, "Assets", "msi.swr");
			PropsFileVariableReference variable = new PropsFileVariableReference("WindowsDesktopSharedFramework31Version", "3.3.3", msiPath);

			PropsFileUpdater propsUpdater = new PropsFileUpdater();
			PropsUpdateResults? results = propsUpdater.UpdatePropsFiles(Enumerable.Repeat(variable, 1), Environment.CurrentDirectory);

			Assert.IsNotNull(results);
			Assert.IsTrue(results.Outcome);

			if (results.UnrecognizedVariables != null)
			{
				Assert.AreEqual(0, results.UnrecognizedVariables.Count, "Variable was not recognized.");
			}

			Assert.IsNotNull(results.UpdatedVariables);
			Assert.AreEqual(1, results.UpdatedVariables.Count);
			Assert.IsTrue(results.UpdatedVariables.First().Value.Contains(variable), "Supplied variable is not among the updated.");

			Assert.IsNotNull(results.ModifiedFiles, "No props file was updated.");
			Assert.AreEqual(1, results.ModifiedFiles.Count, "No props file was updated.");
			Assert.IsTrue(results.ModifiedFiles[0].Succeeded);
			Assert.IsNull(results.ModifiedFiles[0].Exception);
			Assert.AreEqual("dotNetCoreVersions.props", Path.GetFileName(results.ModifiedFiles[0].Path));
		}

		/// <summary>
		/// Uses a variable outside of the props file's scope and checks to see if props file gets updated.
		/// Since props file isn't available for that variable, update shouldn't occur.
		/// </summary>
		[TestMethod]
		public void TestPropsFileScope()
		{
			// swr is in the root folder while props file will be in a child folder.
			string msiPath = Path.Combine(Environment.CurrentDirectory, "msi.swr");
			PropsFileVariableReference variable = new PropsFileVariableReference("WindowsDesktopSharedFramework31Version", "3.3.3", msiPath);

			PropsFileUpdater propsUpdater = new PropsFileUpdater();
			PropsUpdateResults? results = propsUpdater.UpdatePropsFiles(Enumerable.Repeat(variable, 1), Environment.CurrentDirectory);

			Assert.IsNotNull(results);
			Assert.IsTrue(results.Outcome);

			Assert.IsNotNull(results.UnrecognizedVariables);
			Assert.AreEqual(1, results.UnrecognizedVariables.Count);
			Assert.AreEqual(variable.Name, results.UnrecognizedVariables[0].Name);

			Assert.AreEqual(0, results.UpdatedVariables.Count);
			Assert.AreEqual(0, results.ModifiedFiles.Count);
		}

		/// <summary>
		/// Attempts to deduce the value of a variable in an swr using a publicly available nuget package.
		/// </summary>
		[TestMethod]
		public void TestVariableValueDeduce()
		{
			// Generate test default.config
			string testConfig = @"<?xml version=""1.0"" encoding=""us-ascii""?>
<corext>
	<packages>
		<package id=""runtime.win-x64.Microsoft.NETCore.DotNetAppHost"" version=""unimportant"" link=""path\to\extract"" />
	</packages>
</corext>";
			string defaultConfigPath = Path.GetTempFileName();
			File.WriteAllText(defaultConfigPath, testConfig);

			// Load default.config
			DefaultConfigUpdater defaultConfigUpdater = new DefaultConfigUpdater();
			bool configLoadResult = defaultConfigUpdater.TryLoad(defaultConfigPath, out string error);

			Assert.IsTrue(configLoadResult, "Loading default.config failed.");

			// Load msi.swr
			string swrPath = Path.Combine(Environment.CurrentDirectory, "Assets", "msi.swr");
			SwrFileReader swrFileReader = new SwrFileReader(4);

			SwrFile[] loadedSwrFiles = swrFileReader.LoadSwrFiles(Environment.CurrentDirectory);
			SwrFile swrFile = loadedSwrFiles.First(s => s.Path == swrPath);

			PropsVariableDeducer variableDeducer = new PropsVariableDeducer("https://api.nuget.org/v3/index.json", null);
			List<PropsFileVariableReference>? results = variableDeducer.DeduceVariableValues(defaultConfigUpdater,
				new[] { new PackageUpdateResult("runtime.win-x64.Microsoft.NETCore.DotNetAppHost", "unimportant", "3.1.3") },
				new SwrFile[] { swrFile });

			Assert.IsNotNull(results);
			Assert.IsTrue(results.Any(r => r.ReferencedFilePath == swrPath), "Cannot find the value of variable in given swr.");
			Assert.IsTrue(results.Any(r => r.ReferencedFilePath == swrPath && r.Name == "AspNetCoreTargetingPack30Version"), "Cannot find the correct variable.");
			Assert.IsTrue(results.Any(r => r.ReferencedFilePath == swrPath && r.Name == "AspNetCoreTargetingPack30Version" && r.Value == "apphost"), "Wrong value was found for the variable.");
		}
	}
}
