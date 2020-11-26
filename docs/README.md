# InsertionsClient
The InsertionsClient, .NET Core command line executable, is a tool that is used to insert a [drop](https://github.com/dotnet/arcade/blob/master/Documentation/Darc.md#gathering-a-build-drop) into Visual Studio by updating _default.config_, _.packageconfig_ and _.props_ files in Visual Studio repo. 

The tool works locally, meaning that the Visual Studio repo should already be checked-out and available on the file system. Similarly, the changes made by the tool should be manually committed and pushed back to the repo.

**Table of Contents**
- [How it works](#howitworks)
- [Input](#input)
- [Logging](#logging)
- [Usage](#usage)
- [Output](#output)
- [Code Base](#codebase)

### [How it works](#how-it-works)
Though there are details to it, what InsertionsClient does can be summarized in the following steps:
1. Iterates through the assets in the manifest file and matches them with the NuGet packages listed in _default.config_ and _.packageconfig_ files.
1. Version numbers of the matching NuGet packages in config files are updated with the version numbers in the _manifest.json_ file.
1. If an access token is specified with **-a:** switch, binaries for the updated packages are downloaded. Contents of the packages are used to update the values of properties defined under `PackagePreprocessorDefinitions` tag in .props files.
1. Modified .props files are saved.
1. Updated _default.config_ and _.packageconfig_ files are saved.

### [Input](#input)
This section explains the options you can specify when invoking the tool. Although there are many, only two of them are required.

| Switch | Description | Is Optional| Example |
| :-- | :-- | :-- |:-- |
|**-d:** |Path to the default.config file.| :x: | `-d:c:\default.config`|
|**-m:** |Path to the manifest.json file or to the containing directory.| :x: | `-m:c:\files\manifest.json`<br/>or<br/> `-m:c:\files` |
|**-i:** |Path to ignored packages file.| :heavy_check_mark: | `-i:c:\files\ignore.txt` |
|**-idut** |Indicates that the packages relevant to the .NET Dev UX team are ignored. If both set, **-i:** option overrides this switch.|:heavy_check_mark:| `-idut`|
|**-wl:** |Path to the allowlist file. If this is specified, only the packages listed in this file will be updated. Each line in the file represents a regex pattern that will potentially match one or more package IDs. This switch will be ignored if the given file is empty.| :heavy_check_mark: | `-wl:c:\files\allowlist.txt` |
|**-p**: |Path to the directory to search for .props files. If left unspecified, all the .props files under src\SetupPackages in the local VS repo will be searched.| :heavy_check_mark: |`-p:C:\VS\src\SetupPackages`|
|**-a**: |Personal access token with "read" access to the packages in the [VS feed](https://pkgs.dev.azure.com/devdiv/_packaging/VS-CoreXtFeeds/nuget/v3/index.json). If not specified, props files will not be updated.| :heavy_check_mark: | `-a:vv8ofhtojf7xuhehrFxq9k5zvvxstrqg2dzsedhlu757` |
|**-w:** |Maximum allowed duration in seconds for completing the insertions, excluding downloads.| :heavy_check_mark: | `-w:60` |
|**-ds:** |Maximum allowed duration in seconds that can be spent downloading nuget packages | :heavy_check_mark: | `-ds:240` |
|**-c:** |Maximum level of concurrency.| :heavy_check_mark: | `-c:10` |
|**-bf:** |Filter string to specify which builds from the manifest will be inserted.| :heavy_check_mark: | `-bf:repo=.*core-setup` |

#### More on **-m:**
It is also possible to input multiple manifest files. To do this, use semicolon (;) to separate the paths as such:
 ```
-m:c:\files\manifest.json;\relative\folder\\;c:\another\fullpath\manifest.json
```

 If the same package is updated from multiple manifest files, which manifest file will be used to set the final value is nondeterministic.

#### More on **-bf:**
A simple build filter can be written as follows:

```
-bf:repo=.*core-setup
```

Right side of the equals sign is a regular expression that should fully match the value of the build property that is given on the left side. Thus, this filter only inserts builds where the _repo_ property ends with the word _core-setup_.

The previous example contained only one rule: `repo=.*core-setup`. If you want to specify multiple rules, you can separate them with a comma. Such as:
```
-bf:repo=.*core-setup,channelId=1299
```

A set of rules separated by commas is called a "ruleset". For a build to pass the filter and get inserted, it should comply with **all** the rules within any ruleset.

A more complicated example of this could be:
```
-bf:repo=.*core-setup,channelId=1299;repo=.*,channelId=972
```

Which means that a build can be inserted if:

  a. The repo name ends with "core-setup" and the channels list contains a channel with the id "1299"

  b. Or, the repo name can be anything, but one of the channel ids should be "972"

As you can see, multiple rulesets can be specified using semicolons. A build only needs to comply with one of the rulesets to be inserted.
    
For each rule, the word left of the `=` sign represents a build property. Build property can have the following values: `repo`, `commit`, `branch`,`buildNumber`. There are also two special properties named `channel` and `channelId`. If `channel` is used, then the regular expression on the right side of the equals sign should match with the name of any of the channels of the build. Similarly, `channelId` is used to select builds where any of the channel ids of a build should match the given regular expression.

_Warnings_
1. NO SPACES ALLOWED IN EITHER default.config OR manifest.json FILE PATHS
1. NO SPACES ALLOWED IN props file search directory
1. The default duration & concurrency values should suffice
1. When using build filters with -bf switch, special characters in the regular expression should be properly escaped.

### [Logging](#logging)
* The tool creates a log, detailing every step taken.
* The logs are placed in the **Logs** folder under the current directory.
* Full path to the generated log file is display at the end of the program.

#### Log Format
Each log line details:
1. The time stamp when the message was logged
1. the id of the thread where the message was logged
1. the message string

#### Sample Log Lines
<pre>
12-3-2020 11:59:16.114133|thread:1|CMD line param. Specified default.config: C:\Users\bozturk\source\repos\VS\.corext\Configs\default.config
12-3-2020 11:59:16.122993|thread:1|CMD line param. Specified manifest.json: C:\Users\bozturk\source\repos\InsertionsClient\tests\InsertionsClientTest\Assets\manifest.json
12-3-2020 11:59:16.349088|thread:1|De-serialized 19 builds from manifest.json.
12-3-2020 11:59:16.380310|thread:1|Loading default.config content from C:\Users\bozturk\source\repos\VS\.corext\Configs\default.config.
12-3-2020 11:59:16.412066|thread:1|Loaded default.config content.
12-3-2020 11:59:16.419213|thread:1|Loading content of .packageconfig at C:\Users\bozturk\source\repos\VS\.corext\Configs\Microsoft.Developer.IdentityService\IdentityService.packageconfig.
12-3-2020 11:59:16.421827|thread:1|Loaded .packageconfig content.
</pre>

### [Usage](#usage)
1. Launch a command line session.
1. Navigate to the location of _InsertionsClient.exe_
1. Alternatively, add the directory containing the _InsertionsClient.exe_ to the PATH environment variable.
1. Launch _InsertionsClient.exe_ with proper arguments.
#### Examples
The examples below rely on the following conditions:
1. _InsertionsClient.exe_ is located under the directory "\tools"
1. _default.config_  is located under the directory "\repos\Assets"
1. _manifest.json_  is located under the directory "\repos\Assets"

#### Opting to Specify File with NuGet Packages to Ignore
If you have an _ignored.txt_ file located under "\repos\Assets", you can run the following command to do an insertion while preserving the version numbers of packages specified in the _ignored.txt_:
```pwsh
$ \tools\InsertionsClient.exe -d:\repos\Assets\default.config -m:\repos\Assets\manifest.json -i:\repos\Assets\ignored.txt
```
#### Restricting the Affected Packages With an Allowlist
Contents of a simple _allowlist.txt_ can be as follows:
`^VS\.Redist\.Common\.NetCore\.SharedFramework\.(x86|x64)\.[0-9]+\.[0-9]+$`

If you have an _allowlist.txt_ file located under "\repos\Assets", the following command can be used:
```pwsh
$ \tools\InsertionsClient.exe -d:\repos\Assets\default.config -m:\repos\Assets\manifest.json -wl:\repos\Assets\allowlist.txt
```

#### Opting to Ignore .NET Dev UX NuGet Packages
```pwsh
$ \tools\InsertionsClient.exe -d:\repos\Assets\default.config -m:\repos\Assets\manifest.json -idut
```
#### Opting Not to Ignore any NuGet Packages
```pwsh
$ \tools\InsertionsClient.exe -d:\repos\Assets\default.config -m:\repos\Assets\manifest.json
```
#### Specifying an Access Token to Update .props Files
```pwsh
$ \tools\InsertionsClient.exe -d:\repos\Assets\default.config -m:\repos\Assets\manifest.json -a:vv8ofhtojf7xuhehroaq9k5zvvxstrqg2dzsedhlu757
```
#### Specifying a .props File Search Directory
```pwsh
$ \tools\InsertionsClient.exe -d:\repos\Assets\default.config -m:\repos\Assets\manifest.json -a:vv8ofhtojf7xuhehrFxq9k5zvvxstrqg2dzsedhlu757 -p:C:\VS\src\SetupPackages\DotNetCoreSDK
```

### [Output](#output)
_InsertionsClient.exe_ outputs the results of the running operations both to a persistent log file and to the console output.

Every successfully updated package version in a _default.config_ or _.packageconfig file has a corresponding message such as:
<pre>
12-3-2020 11:59:16.694366|thread:6|Package VS.Redist.Common.WindowsDesktop.SharedFramework.x64.3.1 was updated to version 3.1.2-servicing.20067.4
</pre>

**Completion statistics** summarize the time spent on each asset in the manifest file.
<pre>
Statistics:

ExactMatch - Update for case of matching manifest.json assets with multiple verions; where version of the exact matching NuGet asset was selected
No. Items: 31
Average: 0.0041-ms
Minimum: 0.0005-ms
Maximum: 0.0269-ms

NoMatch - No matching manifest.json assets for a given default.config NuGet
No. Items: 528
Average: 0.0042-ms
Minimum: 0.0003-ms
Maximum: 0.0010-ms
</pre>


**Completion Summary** summarizes the total duration of the operation and the updated package versions as follows:
<pre>
Duration: 480.00-ms.
Successful updates: 27.
Updated default.config NuGet package versions...
        VS.Redist.Common.AspNetCore.SharedFramework.x64.3.1, version: 3.1.2-servicing.20068.1
        VS.Redist.Common.AspNetCore.SharedFramework.x86.3.1, version: 3.1.2-servicing.20068.1
        ...
</pre>

### [Codebase](#codebase)
Information about the codebase can be found in [Codebase](docs/Codebase.md) page.