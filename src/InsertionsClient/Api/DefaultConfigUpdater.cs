// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Common.Constants;
using Microsoft.Net.Insertions.Models;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Linq;

namespace Microsoft.Net.Insertions.Api
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

        private readonly ConcurrentDictionary<XDocument, byte> _modifiedDocuments;

        private readonly ConcurrentDictionary<string, XElement> _packageXElements;

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

            _packageconfigWriteSettings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = true,
                Indent = true
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
            error = null;

            if (!File.Exists(defaultConfigPath))
            {
                error = $"{InsertionConstants.DefaultConfigFile} file was not found at path {defaultConfigPath}";
                return false;
            }

            XDocument defaultConfigXml;
            try
            {
                Trace.WriteLine($"Loading {InsertionConstants.DefaultConfigFile} content from {defaultConfigPath}.");
                defaultConfigXml = XDocument.Load(defaultConfigPath, LoadOptions.SetLineInfo);
                Trace.WriteLine($"Loaded {InsertionConstants.DefaultConfigFile} content.");
            }
            catch(Exception e)
            {
                Trace.WriteLine($"Loading of {InsertionConstants.DefaultConfigFile} content has failed with exception:{Environment.NewLine} {e.ToString()}");
                return false;
            }

            _documentPaths[defaultConfigXml] = defaultConfigPath;
            LoadPackagesFromXml(defaultConfigXml);

            // Find xml elements that define additional .packageconfig in them.
            IEnumerable<XElement> additionalConfigParents = defaultConfigXml.Descendants(ElementNameAdditionalConfigsParent);
            if(additionalConfigParents != null)
            {
                string configsDirectory = Path.GetDirectoryName(defaultConfigPath);

                foreach(var packageconfigXElement in additionalConfigParents.SelectMany(p => p.Elements(ElementNameAdditionalConfig)))
                {
                    string configFileRelativePath = packageconfigXElement.Attribute("name")?.Value;

                    if(string.IsNullOrWhiteSpace(configFileRelativePath))
                    {
                        Trace.WriteLine($"{InsertionConstants.DefaultConfigFile} lists a .packageconfig under {ElementNameAdditionalConfigsParent} tag, but the .packageconfig has no name.");
                        continue;
                    }

                    string configFileAbsolutePath = Path.Combine(configsDirectory, configFileRelativePath);

                    if(!File.Exists(configFileAbsolutePath))
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
                    catch(Exception e)
                    {
                        Trace.WriteLine($"Loading of .packageconfig file has failed with exception{Environment.NewLine}{e.ToString()}");
                        continue;
                    }

                    _documentPaths[packageConfigXDocument] = configFileAbsolutePath;
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
        /// <returns>True if package was found. False otherwise.</returns>
        /// <remarks>This method is safe to call simultaneously from multiple threads.</remarks>
        public bool TryUpdatePackage(string packageId, string version, out string existingVersion)
        {
            if (!_packageXElements.TryGetValue(packageId, out var xElement))
            {
                existingVersion = null;
                return false;
            }

            existingVersion = xElement.Attribute("version").Value;
            if (existingVersion == version)
            {
                // Package was found. But no version update was necessary
                return true;
            }

            xElement.Attribute("version").Value = version;

            // Store the document. Store a junk value (0) with it, because we have to.
            _modifiedDocuments[xElement.Document] = 0;
            return true;
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

            foreach(var document in _modifiedDocuments.Keys)
            {
                var savePath = _documentPaths[document];
                Trace.WriteLine($"Saving modified config file: {savePath}");
                try
                {
                    using(XmlWriter writer = XmlWriter.Create(savePath, _packageconfigWriteSettings))
                    {
                        document.Save(writer);
                    }
                    results[arraySaveIndex++] = new FileSaveResult(savePath);
                    Trace.WriteLine("Save success.");
                }
                catch(Exception e)
                {
                    results[arraySaveIndex++] = new FileSaveResult(savePath, e);
                    Trace.WriteLine($"Save failed with exception:{e.ToString()}");
                }
            }

            return results;
        }
    
        private void LoadPackagesFromXml(XDocument xDocument)
        {
            foreach (var packageXElement in xDocument.Descendants(ElementNamePackage))
            {
                string packageId = packageXElement.Attribute("id")?.Value;

                if (string.IsNullOrWhiteSpace(packageId))
                {
                    Trace.WriteLine($"Xml file contains a package with no id. Line {((IXmlLineInfo)packageXElement).LineNumber} at file {_documentPaths[xDocument]}");
                    continue;
                }

                if (_packageXElements.TryGetValue(packageId, out var pElement))
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