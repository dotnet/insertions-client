## <a href="#codebase">Codebase</a>
InsertionsClient solution consists of 2 projects for the application logic and 2 projects for their respective tests.

Besides the solution, the documentation and the `.yml` file for the build pipeline also reside in the repo at https://github.com/dotnet/insertions-client.

This section covers the highlights of both `Core` and the `Console` projects as well as the `.yml` file for the build pipeline.

**Table of Contents**
- [Microsoft.DotNet.InsertionsClient.Core](#core-project)
  - [IInsertionApi Interface](#iinsertionapi-interface)
  - [InsertionApi Class](#insertionapi-class)
- [Microsoft.DotNet.InsertionsClient.Console](#console-project)
  - [Program Class](#program-class)
  - [Logging](#logging)
- [Build Pipeline](#build-pipeline)


### [Core Project](#core-project)
Core is the project where the main insertion logic resides in. The project exposes an interface (`IInsertionApi`) so that the code can be consumed by other projects and invoked like an API. 
- `Console` project is an example of this. It receives the inputs from the console and invokes the api accordingly. See [Input](docs/README.md#Input) section for more details on how to use the tool from command line.

#### [IInsertionApi Interface](#iinsertionapi-interface)
Defines how to interact with the library to update versions of targeted assets, such as _default.config_.
```csharp
UpdateResults UpdateVersions(
            IEnumerable<string> manifestFiles,
            string defaultConfigFile,
            IEnumerable<Regex> whitelistedPackages,
            ImmutableHashSet<string>? packagesToIgnore,
            string? accessToken,
            string? propsFilesRootDirectory,
            Predicate<Build>? buildFilter);
```
- __manifestFiles:__ The paths to all the manifest.json files to be inserted.
- __defaultConfigFile:__ Full path to _default.config_ file.
- __whitelistedPackages:__ Regex patterns matching with IDs of the packages that are allowed to be updated. If the set is empty,
 any package is allowed be updated unless specified in packagesToIgnore argument.
- __packagesToIgnore:__ IDs of the packages to be ignored.
- __accessToken:__ Access token to be used when connecting to VS feed.
- __propsFilesRootDirectory:__ Directory that will be searched for `.props` files.
- __buildFilter:__ Predicate to determine if a build should be included in the insertion process.
- Returns: an `UpdateResults` instance, detailing the outcome of the operation.

In a simple case, this method can be invoked in the following way:
```csharp
 IInsertionApiFactory apiFactory = new InsertionApiFactory();
 IInsertionApi api = apiFactory.Create(MaxWaitDuration, MaxDownloadDuration, MaxConcurrency);
 api.UpdateVersions(new []{"\\Assets\\manifest.json"},
    "\\\\VS\\.corext\\default.config",
    Enumerable.Empty<Regex>(),
    ImmutableHashSet.Create("VS.ExternalAPIs.MSBuild"),
    null, null, null);
```

#### [InsertionApi Class](#insertionapi-class)
`InsertionApi` is the only implementation of `IInsertionApi` in the solution. The main logic executes the following steps in order:
1. Load _default.config_ file into memory. All the operations on _default.config_ is handled in `DefaultConfigUpdater` class.
1. Load manifest.json files.
1. Determine the updates to the _default.config_ file. This operation is executed in parallel for each asset defined in manifest files.
1. Determine the updates to `.props` files.
    1. Determine all the `.swr` files.
    2. Search the contents of `.swr` files for variables of the form `$(variable_name)`.
    3. Download the packages updated in step 3 into memory.
    4. Determine the value of the variables by comparing the _file names in the nuget packages_ with _the file names listed in swr payloads_.
1. In the declaring `.props` files, replace the value of the variables with the values found in the previous step.
1. Flush changes to `.props` files.
1. Flush changes to `default.config` and `.packageconfig` files.

### [Console Project](#console-project)
The console project contains the command line user interface codebase and logging.

#### [Program Class](#program-class)
- The Program class contains the assembly entry point
and is the most interesting class in the assembly
- The class defines the switches, explained in the _[Input](docs/README.md#input)_ section.

#### [Logging](#logging)
The application's logging solution relies on _System.Diagnostics.Trace_ events & registers _ConsoleTraceListener_ 
and `TextWriterTraceListener` listeners, respectively.  See the _[Output](docs/README.md#output)_ section for more details.

### [Build Pipeline](#build-pipeline)
The build definition resides in the [`azure-pipelines.yml`](/azure-pipelines.yml) file.
There are two build pipelines for InsertionsClient:
- [Public pipeline](https://dev.azure.com/dnceng/public/_build?definitionId=846&_a=summary). Runs the validation builds for PRs.

- [Internal pipeline](https://dev.azure.com/dnceng/internal/_build?definitionId=847&_a=summary). Besides validation, generates and publishes nuget packages to the [feed](https://dev.azure.com/dnceng/public/_packaging?_a=package&feed=dotnet-eng). There are two packages published:
  - [Microsoft.DotNet.InsertionsClient.Core](https://dev.azure.com/dnceng/public/_packaging?_a=package&feed=dotnet-eng&package=Microsoft.DotNet.InsertionsClient.Core&protocolType=NuGet)
  - [Microsoft.DotNet.InsertionsClient.Console](https://dev.azure.com/dnceng/public/_packaging?_a=package&feed=dotnet-eng&package=Microsoft.DotNet.InsertionsClient.Console&protocolType=NuGet)

  Major, minor and patch version numbers are specified in the `azure-pipelines.yml` file. A unique build number is calculated for each run and appended to the version number.



