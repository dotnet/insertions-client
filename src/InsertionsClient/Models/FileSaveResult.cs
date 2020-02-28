using System;

namespace Microsoft.Net.Insertions.Models
{
    /// <summary> Stores the result of a file save operation.
    public struct FileSaveResult
    {
        /// <summary> Path to the file that was saved. </summary>
        public readonly string Path;

        /// <summary> Exception that was thrown when saving the file, if exists. </summary>
        public readonly Exception Exception;

        /// <summary> Was file save operation successful? If yes, <see cref="Exception"/>
        /// property holds a reference to the exception that was thrown. </summary>
        public bool DidSucceed => Exception == null;

        /// <summary> Creates an instance of FileSaveResult </summary>
        public FileSaveResult(string path, Exception exception = null)
        {
            Path = path;
            Exception = exception;
        }
    }
}