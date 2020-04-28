// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.Net.Insertions.Props.Models
{
    /// <summary>
    /// This type represents a props file that contains the variables
    /// to be used during VS build process.
    /// </summary>
    public sealed class PropsFile
    {
        /// <summary>
        /// Matches a variable assignement line.
        /// First matched group is the variable name, second one is the value.
        /// </summary>
        /// <example>NetCoreSharedHostVersion=3.1.2;</example>
        private static Regex _variableAssignmentRegex = new Regex(@"\b(\w*)=([\w|.|-]*);");

        /// <summary>
        /// Creates an instance of PropsFile.
        /// </summary>
        /// <param name="path">Path to the file on disk.</param>
        internal PropsFile(string path)
        {
            Path = path;
        }

        /// <summary>
        /// Path to this file on disk.
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Has content of this file changes since we loaded it?
        /// </summary>
        public bool isDirty { get; private set; }

        private XDocument? _xDocument { get; set; }

        private bool _isLoaded => _xDocument != null;

        /// <summary>
        /// Attempts to find given variable in the file. Changes the value if found.
        /// </summary>
        /// <param name="variableName">Name of the variable to change</param>
        /// <param name="value">New value of the variable</param>
        /// <param name="value">Previous value of the variable, if found.</param>
        /// <returns>True if variable was found. False otherwise</returns>
        public bool TryUpdateVariable(string variableName, string value, out string existingValue)
        {
            if (!_isLoaded && !LoadDocument())
            {
                Trace.WriteLine($"Updating variable \"{variableName}\" has failed.");
                existingValue = string.Empty;
                return false;
            }

            foreach (XNode node in _xDocument!.Descendants(_xDocument.Root.Name.Namespace + "PackagePreprocessorDefinitions").Nodes())
            {
                if (node.NodeType != XmlNodeType.Text)
                {
                    // PreprocessorDefinitions are always defined in text nodes.
                    continue;
                }

                XText textNode = (XText)node;
                string textValue = textNode.Value;
                foreach (Match? match in _variableAssignmentRegex.Matches(textNode.Value))
                {
                    if (match!.Groups[1].Value == variableName)
                    {
                        existingValue = match!.Groups[2].Value;

                        if (existingValue == value)
                        {
                            // Value is the same, no need to set dirty.
                            return true;
                        }

                        isDirty = true;
                        textNode.Value = $"{textValue.Substring(0, match.Index)}{variableName}={value};{textValue.Substring(match.Index + match.Length)}";
                        return true;
                    }
                }
            }

            existingValue = string.Empty;
            return false;
        }

        /// <summary>
        /// Saves the XmlDocument back to disk.
        /// </summary>
        /// <returns>The result of the operation.
        /// An instance is returned if there was an attempt to save 
        /// file to disk. If there are no changes to the file and no
        /// disk operation is needed, returns null value.
        /// </returns>
        public FileSaveResult? Save()
        {
            if (_isLoaded == false || !isDirty)
            {
                // We don't have a better data in memory
                // Data on the disk is already what is expected. Consider this done.
                return null;
            }

            try
            {
                _xDocument!.Save(Path);
                return new FileSaveResult(Path);
            }
            catch (Exception e)
            {
                return new FileSaveResult(Path, e);
            }
        }

        private bool LoadDocument()
        {
            if (!File.Exists(Path))
            {
                Trace.WriteLine($"Props file does not exist at location: {Path}");
                return false;
            }

            try
            {
                _xDocument = XDocument.Load(Path);
                isDirty = false;
                return true;
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Failed to load xml document at path: {Path}");
                Trace.WriteLine(e.ToString());
                return false;
            }
        }
    }
}
