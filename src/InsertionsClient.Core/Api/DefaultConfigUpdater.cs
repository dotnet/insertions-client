﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.DotNet.InsertionsClient.Common.Constants;
using Microsoft.DotNet.InsertionsClient.Models;
using NuGet.Versioning;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.DotNet.InsertionsClient.Api
{
    /// <summary>
    /// Manages loading and updating &quot;default.config&quot; file and &quot;.packageconfig&quot; files listed in it.
    /// </summary>
    internal sealed class DefaultConfigUpdater
    {
        private const string ElementNamePackage = "package";

        private const string ElementNameAdditionalConfigsParent = "additionalPackageConfigs";

        private const string ElementNameAdditionalConfig = "config";

        private readonly Dictionary<XDocument, string> _documentPaths;

        //A ConcurrentDictionary is thread safe, otherwise a HashSet would be a better option
        private readonly ConcurrentDictionary<XDocument, byte> _modifiedDocuments;

        private readonly ConcurrentDictionary<string, XElement> _packageXElements;

        private readonly XmlWriterSettings _defaultconfigWriteSettings;

        private readonly XmlWriterSettings _packageconfigWriteSettings;

        /// <summary>
        /// Creates an instance of DefaultConfigUpdater
        /// </summary>
        public DefaultConfigUpdater() : this(concurrencyLevel: 8) { }

        /// <summary>
        /// Creates an instance using the given concurrency level for initializing internal
        /// concurrent collections.
        /// </summary>
        /// <param name="concurrencyLevel">Excpected concurrency level for multithreaded access.</param>
        public DefaultConfigUpdater(int concurrencyLevel)
        {
            _documentPaths = new Dictionary<XDocument, string>(16);
            _modifiedDocuments = new ConcurrentDictionary<XDocument, byte>(concurrencyLevel, 17);
            _packageXElements = new ConcurrentDictionary<string, XElement>(concurrencyLevel, 1021);

            _defaultconfigWriteSettings = new XmlWriterSettings()
            {
                Indent = true,
                Encoding = Encoding.ASCII
            };

            _packageconfigWriteSettings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = true,
                Indent = true,
                Encoding = Encoding.ASCII
            };
        }

        /// <summary>
        /// Loads the config file at the given path along with all the .packageconfig files in it.
        /// </summary>
        /// <param name="defaultConfigPath">Path to the &quot;default.config&quot; file.</param>
        /// <param name="error">Description of the error occured during load.</param>
        /// <returns>True if the operation is successful. False otherwise.</returns>
        /// <remarks>This method is not thread-safe.</remarks>
        public bool TryLoad(string defaultConfigPath, out string error)
        {
            error = string.Empty;

            if (!File.Exists(defaultConfigPath))
            {
                error = $"{InsertionConstants.DefaultConfigFile} file was not found at path {defaultConfigPath}";
                return false;
            }

            XDocument defaultConfigXml;
            try
            {
                Trace.WriteLine($"Loading {InsertionConstants.DefaultConfigFile} content from {defaultConfigPath}.");
                defaultConfigXml = XDocument.Load(defaultConfigPath, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
                Trace.WriteLine($"Loaded {InsertionConstants.DefaultConfigFile} content.");
            }
            catch (Exception e)
            {
                error = e.Message;
                Trace.WriteLine($"Loading of {InsertionConstants.DefaultConfigFile} content has failed with exception:{Environment.NewLine} {e.ToString()}");
                return false;
            }

            // Stores default.config into _documentPaths
            _documentPaths[defaultConfigXml] = defaultConfigPath;
            // Stores packages within default.config into _packageXElements
            LoadPackagesFromXml(defaultConfigXml);

            // Find xml elements that define additional .packageconfig in them going through each element under <additionalPackageConfigs>
            IEnumerable<XElement> additionalConfigParents = defaultConfigXml.Descendants(ElementNameAdditionalConfigsParent);
            if (additionalConfigParents != null)
            {
                string configsDirectory = Path.GetDirectoryName(defaultConfigPath) ?? string.Empty;

                foreach (XElement packageconfigXElement in additionalConfigParents.SelectMany(p => p.Elements(ElementNameAdditionalConfig)))
                {
                    string? configFileRelativePath = packageconfigXElement.Attribute("name")?.Value;

                    if (string.IsNullOrWhiteSpace(configFileRelativePath))
                    {
                        Trace.WriteLine($"{InsertionConstants.DefaultConfigFile} lists a .packageconfig under {ElementNameAdditionalConfigsParent} tag, but the .packageconfig has no name.");
                        continue;
                    }

                    string configFileAbsolutePath = Path.Combine(configsDirectory, configFileRelativePath).Replace('\\', '/');

                    if (!File.Exists(configFileAbsolutePath))
                    {
                        Trace.WriteLine($"File for the .packageconfig listed under {InsertionConstants.DefaultConfigFile} was not found on disk. Path: {configFileAbsolutePath}");
                        continue;
                    }

                    XDocument packageConfigXDocument;
                    try
                    {
                        Trace.WriteLine($"Loading content of .packageconfig at {configFileAbsolutePath}.");
                        packageConfigXDocument = XDocument.Load(configFileAbsolutePath);
                        Trace.WriteLine($"Loaded .packageconfig content.");
                    }
                    catch (Exception e)
                    {
                        Trace.WriteLine($"Loading of .packageconfig file has failed with exception{Environment.NewLine}{e.ToString()}");
                        continue;
                    }
                    // Stores [file].packageconfig into _documentPaths
                    _documentPaths[packageConfigXDocument] = configFileAbsolutePath;
                    // Stores packages from each .packageconfig into _packageXElements
                    LoadPackagesFromXml(packageConfigXDocument);
                }
            }

            return true;
        }

        /// <summary>
        /// Attempts to find the package with given id and update its version number.
        /// </summary>
        /// <param name="packageId">Id of the package to change the version of</param>
        /// <param name="version">Version number to assign</param>
        /// <returns>
        /// True if package was found and the version number corresponds to a valid semantic version.
        /// False otherwise.
        /// </returns>
        /// <remarks>This method is safe to call simultaneously from multiple threads.</remarks>
        public bool TryUpdatePackage(string packageId, NuGetVersion version, out NuGetVersion? existingVersion)
        {
            if (!_packageXElements.TryGetValue(packageId, out XElement? xElement))
            {
                existingVersion = default;
                return false;
            }

            lock (xElement)
            {
                string existingVersionStr = xElement.Attribute("version").Value;

                if (!NuGetVersion.TryParse(existingVersionStr, out existingVersion))
                {
                    return false;
                }

                if (existingVersion >= version)
                {
                    // Package was found. But no version update was necessary
                    return true;
                }

                // Update the version
                xElement.Attribute("version").Value = version.ToFullString();
            }

            // Store the document. Store a junk value (0) with it, because we have to.
            _modifiedDocuments[xElement.Document] = 0;
            return true;
        }

        /// <summary>
        /// Returns the link attribute defined in the xml attributes of the package.
        /// </summary>
        /// <param name="packageId">Id of the package, link of which will be returned.</param>
        /// <returns>Returns the value of the link attribute. 
        /// Returns null, if attribute or package is not found.</returns>
        /// <remarks>This method is safe to call simultaneously from multiple threads.</remarks>
        public string? GetPackageLink(string packageId)
        {
            if (!_packageXElements.TryGetValue(packageId, out XElement? xElement))
            {
                // Package was not found.
                Trace.WriteLine($"Package {packageId} was not found in default.config/.packageconfig files.");
                return null;
            }

            XAttribute linkAttribute = xElement.Attribute("link");
            if (linkAttribute == null)
            {
                // Attribute was not found
                Trace.WriteLine($"Package {packageId} does not have a link attribute in {_documentPaths[xElement.Document]}:{((IXmlLineInfo)xElement).LineNumber}");
                return null;
            }

            return linkAttribute.Value;
        }

        /// <summary>
        /// Saves all the modified default.config and .packageconfig files to disk.
        /// </summary>
        /// <returns> Results of the save operations. </returns>
        /// <remarks>This method is not thread-safe.</remarks>
        public FileSaveResult[] Save()
        {
            FileSaveResult[] results = new FileSaveResult[_modifiedDocuments.Count];
            int arraySaveIndex = 0;

            foreach (XDocument document in _modifiedDocuments.Keys)
            {
                string savePath = _documentPaths[document];
                Trace.WriteLine($"Saving modified config file: {savePath}");
                try
                {
                    string extension = Path.GetExtension(savePath).ToLowerInvariant();

                    XmlWriterSettings writeSettings = extension == ".packageconfig"
                        ? _packageconfigWriteSettings
                        : _defaultconfigWriteSettings;

                    using XmlWriter writer = XmlWriter.Create(savePath, writeSettings);
                    document.Save(writer);
                    results[arraySaveIndex++] = new FileSaveResult(savePath);
                    Trace.WriteLine("Save success.");
                }
                catch (Exception e)
                {
                    results[arraySaveIndex++] = new FileSaveResult(savePath, e);
                    Trace.WriteLine($"Save failed with exception:{e.ToString()}");
                }
            }

            return results;
        }

        /// <summary>
        /// Each individual package gets stored into _packageXElements
        /// </summary>
        private void LoadPackagesFromXml(XDocument xDocument)
        {
            foreach (XElement packageXElement in xDocument.Descendants(ElementNamePackage))
            {
                string? packageId = packageXElement.Attribute("id")?.Value;

                if (string.IsNullOrWhiteSpace(packageId))
                {
                    Trace.WriteLine($"Xml file contains a package with no id. Line {((IXmlLineInfo)packageXElement).LineNumber} at file {_documentPaths[xDocument]}");
                    continue;
                }

                if (_packageXElements.TryGetValue(packageId, out XElement? pElement))
                {
                    Trace.WriteLine($"Duplicate entries were found for package: {packageId}{Environment.NewLine}\t1-Line {((IXmlLineInfo)pElement).LineNumber} at {_documentPaths[pElement.Document]}{Environment.NewLine}\t2-Line {((IXmlLineInfo)packageXElement).LineNumber} at {_documentPaths[xDocument]}");
                    continue;
                }

                if (packageXElement.Attribute("version") == null)
                {
                    Trace.WriteLine($"Package does not have a version attribute.{Environment.NewLine}\tLine {((IXmlLineInfo)packageXElement).LineNumber}{Environment.NewLine}\tFile {_documentPaths[xDocument]}");
                    continue;
                }

                _packageXElements[packageId] = packageXElement;
            }
        }
    }
}