// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Net.Insertions.Models;
using Microsoft.Net.Insertions.Props.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.Net.Insertions.Api.Providers
{
	/// <summary>
	/// Used to find given variables in props files under a given directory and updates their values.
	/// </summary>
	internal class PropsFileUpdater
	{
		/// <summary>
		/// Props files, keyed by their immediate directories.
		/// </summary>
		private readonly Dictionary<string, List<PropsFile>> _propsFiles = new Dictionary<string, List<PropsFile>>();

		/// <summary>
		/// Finds the given variables in props files under the given directory and updates their values.
		/// </summary>
		/// <param name="variables">The variables to be updated</param>
		/// <param name="rootSearchDirectory">Which folder should be searched for props files?</param>
		/// <returns><see cref="UpdateResults"/> object, storing information about the result of the operation.</returns>
		public PropsUpdateResults UpdatePropsFiles(IEnumerable<PropsFileVariableReference> variables, string rootSearchDirectory)
		{
			Load(rootSearchDirectory);

			PropsUpdateResults results = new PropsUpdateResults();

			foreach (PropsFileVariableReference variableReference in variables)
			{
				string referencedDirectory = Path.GetDirectoryName(variableReference.ReferencedFilePath) ?? string.Empty;
				if (!TryUpdateValue(variableReference.Name, variableReference.Value, referencedDirectory, out PropsFile? updatedFile, out string existingValue))
				{
					// Variable was not found.
					results.UnrecognizedVariables.Add(variableReference);
					results.OutcomeDetails = "Some of the variables were not found in any .props files.";
					Trace.WriteLine($"Variable \"{variableReference.Name}\" was not found in any of the .props files.");
					continue;
				}

				if (variableReference.Value == existingValue)
				{
					continue;
				}

				List<PropsFileVariableReference>? modifiedVariables;
				if (!results.UpdatedVariables.TryGetValue(updatedFile!, out modifiedVariables))
				{
					results.UpdatedVariables[updatedFile!] = modifiedVariables = new List<PropsFileVariableReference>();
				}

				modifiedVariables.Add(variableReference);
			}

			foreach (FileSaveResult saveResult in Save())
			{
				results.ModifiedFiles.Add(saveResult);
			}

			results.Outcome = true;
			return results;
		}

		/// <summary>
		/// Finds and preprocessed props file under the given directory.
		/// </summary>
		/// <param name="rootSearchDirectory">Root directory that contains the props files.
		/// Specify the most specific directory as root to prevent searching unnecessary folders.</param>
		private void Load(string rootSearchDirectory)
		{
			string[] paths = Directory.GetFiles(rootSearchDirectory, "*.props", SearchOption.AllDirectories);
			foreach (string propFilePath in paths)
			{
				string directory = Path.GetDirectoryName(propFilePath) ?? "";

				List<PropsFile>? propsInDirectory;
				if (!_propsFiles.TryGetValue(directory, out propsInDirectory))
				{
					_propsFiles[directory] = propsInDirectory = new List<PropsFile>();
				}

				propsInDirectory.Add(new PropsFile(propFilePath));
			}
		}

		/// <summary>
		/// Save the changes made to props files onto disk.
		/// </summary>
		/// <returns>Results of the attempted file operations.</returns>
		private IEnumerable<FileSaveResult> Save()
		{
			foreach (PropsFile file in _propsFiles.SelectMany(filesInFolder => filesInFolder.Value))
			{
				FileSaveResult? result = file.Save();

				if (result == null)
				{
					// No changes were made to disk
					continue;
				}

				yield return result.Value;
			}
		}

		/// <summary>
		/// Attempts to find the given variable in props files and update its value.
		/// </summary>
		/// <param name="variableName">Name of the variable to search for.</param>
		/// <param name="newValue">New value to be assigned to the variable</param>
		/// <param name="scopeDirectory">The folder containing the file that references the given variable.
		/// Note that a variable can only be referenced if it is defined in the current or in one  of the 
		/// parent directories.</param>
		/// <param name="updatedFile">PropsFile that defines the variable.</param>
		/// <param name="existingValue">Value of the variable before the update, if found.</param>
		/// <returns>True, if variable was found and updated.
		/// False, if variable wasn't found or the existing value was the same as the new one.</returns>
		private bool TryUpdateValue(string variableName, string newValue, string scopeDirectory, out PropsFile? updatedFile, out string existingValue)
		{
			string? searchDirectory = scopeDirectory;
			while (!string.IsNullOrEmpty(searchDirectory))
			{
				if (_propsFiles.TryGetValue(searchDirectory, out List<PropsFile>? closestPropsFiles))
				{
					// Found some props in this directory. Lets try to update
					foreach (PropsFile propFile in closestPropsFiles)
					{
						if (propFile.TryUpdateVariable(variableName, newValue, out existingValue))
						{
							updatedFile = propFile;
							return true;
						}
					}
				}

				// Variable was not found in any of the props files. Search higher directories
				searchDirectory = Path.GetDirectoryName(searchDirectory);
			}

			// Variable was not found in any of the props files.
			updatedFile = null;
			existingValue = string.Empty;
			return false;
		}
	}
}
