# Overview
**InsertionsClient** updates the versions of NuGet packages in _default.config_ with the corresponding versions specified in _manifest.json_ assets. It also updates the values of properties defined in .props files.

## How does **InsertionsClient** work?
1. Loads into memory the contents of both _default.config_ and _manifest.json_ as well as all the _.packageconfig_ files listed in the _default.config_
1. Searches _default.config_ and _.packageconfig_ files for corresponding NuGet packages for each _manifest.json_ asset
1. For every match, the NuGet version in the config file is updated with that of the corresponding _manifest.json_ asset
1. The updated _default.config_ and _.packageconfig_'s are saved on disk
1. If an access token is specified with **-a:** switch, binaries for the updated packages are downloaded. Content of the packages are used to update the values of properties defined in `PackagePreprocessorDefinitions` tag in .props files
1. Modified .props file are saved on disk
## Input
1. **-d:** Path to the default.config.  Example: 
    > _-d:c:\default.config_
1. **-m:** Path to the manifest.json file or to the containing directory.
   
   The following two examples use the same manifest file:
    > _-m:c:\files\manifest.json_
    > _-m:c:\files\_

   It is also possible to input multiple manifest files. In this case, the files will be processed in order. If the same package is updated from multiple manifest files, version number from the last specified manifest file will be used. To do this, use semicolon (;) to separate the paths as such:
    > _m:c:\files\manifest.json;c:\just\folder\\;c:\another\fullpath\manifest.json_
1. **-i:** Path to ignored packages file [**optional**]. Example: 
    > _-i:c:\files\ignore.txt_
1. **-idut** Indicates that packages relevant to the .NET Dev UX team are ignored [**optional**].  If **-i:** is also set, the file specified with that option is used, superceding **-idut**.
    > _-idut_
1. **-wl:** Path to the whitelist file [**optional**]. If this is specified, only the packages listed in this file will be updated. Each line in the file represents a regex pattern that will potentially match one or more package IDs. This switch will be ignored if the given file is empty.
    > _-wl:c:\files\whitelist.txt_
1. **-p**: Path to the directory to search for .props files [**optional**]. If left unspecified, all the .props files under src\SetupPackages in local VS repo will be searched.
    > _-p:C:\VS\src\SetupPackages_
1. **-a**: Personal access token to access packages in [VS feed](https://pkgs.dev.azure.com/devdiv/_packaging/VS-CoreXtFeeds/nuget/v3/index.json) [**optional**]. If not specified, props files will not be updated.
    > _-a:vv8ofhtojf7xuhehrFxq9k5zvvxstrqg2dzsedhlu757_
1. **-w:** Maximum allowed duration in seconds, excluding downloads [**optional**].  Example: 
    > _-w:60_
1. **-ds:** Maximum allowed duration in seconds that can be spent downloading nuget packages [**optional**].  Example: 
    > _-ds:240_
1. **-c:** Maximum concurrency of default.config version updates [**optional**].  Example:
    > _-c:10_
1. **-bf:** Filter string to specify which builds from the manifest will be inserted.
    
    A simple build filter can be written as follows:
    > _-bf:repo=.*core-setup_

    Right side of the equals sign is a regular expression that should fully match the value of the build property that is given on the left side. Thus, this filter only inserts builds where the repo property ends with the word _core-setup_.

    The previous example contained only one rule: `repo=.*core-setup`. If you want to specify multiple rules, you can separate them with a comma. Such as:
    > _-bf:repo=.*core-setup,channel=release/3.1_

    A set of rules separated by commas is called a "ruleset". For a build to pass the filter and get inserted, it should comply with **all** the rules within any ruleset.

    A more complicated example of this could be:
    > _-bf:repo=.*core-setup,channel=release/3.1;repo=.*,channel=release/5.0_

    Which means that a build can be inserted if:
    a. The repo name ends with core-setup and the channels list contains a channel with the name "release/3.1"
    b. Or, the repo name can be anything, but one of the channel names should be "release/5.0"

    As you can see, multiple rulesets can be specified using semicolons. A build only needs to comply with one of the rulesets to be inserted.
    
    For each rule, the word left of the `=` sign represents a build property. Build property can have the following values: `repo`, `commit`, `branch`,`buildNumber`. There is also a special property named `channel`. If `channel` is used, then the regular expression on the right side of the equals sign should match with any of the channels of the build.

_Warnings_
1. NO SPACES ALLOWED IN EITHER default.config OR manifest.json FILE PATHS
1. NO SPACES ALLOWED IN props file search directory
1. The default duration & concurrency values should suffice
1. When using build filters with -bf switch, special characters in the regular expression should be properly escaped.

## Log
* **InsertionsClient** creates a log detailing every step taken
* The logs are placed in the **Logs** folder relative to the location of the _InsertionsClient.exe_
* Full path to log file is display at the end of the program as well.

### Log Format
Each log line details...
1. The time stamp when the message was logged
1. the id of the thread where the message was logged
1. the message logged

### Sample Log Lines
<pre>
12-3-2020 11:59:16.114133|thread:1|CMD line param. Specified default.config: C:\Users\bozturk\source\repos\VS\.corext\Configs\default.config
12-3-2020 11:59:16.122993|thread:1|CMD line param. Specified manifest.json: C:\Users\bozturk\source\repos\InsertionsClient\tests\InsertionsClientTest\Assets\manifest.json
12-3-2020 11:59:16.349088|thread:1|De-serialized 19 builds from manifest.json.
12-3-2020 11:59:16.380310|thread:1|Loading default.config content from C:\Users\bozturk\source\repos\VS\.corext\Configs\default.config.
12-3-2020 11:59:16.412066|thread:1|Loaded default.config content.
12-3-2020 11:59:16.419213|thread:1|Loading content of .packageconfig at C:\Users\bozturk\source\repos\VS\.corext\Configs\Microsoft.Developer.IdentityService\IdentityService.packageconfig.
12-3-2020 11:59:16.421827|thread:1|Loaded .packageconfig content.
</pre>

## Use
1. Launch command line session
1. Navigate to the location of _InsertionsClient.exe_
1. Alternative, if on WINDOWS, set **InsertionsClient** on the %path% variable to run the application from any location
1. Launch _InsertionsClient.exe_ with the proper parameters
### Examples
The examples below rely on the following conditions...
1. _InsertionsClient.exe_ located on \tools
1. _default.config_ located in \repos\Assets
1. _manifest.json_ located in \repos\Assets

#### Opting to Specify File with NuGet Packages to Ignore
Location of additional needed resources...
1. _ignored.txt_ located in \repos\Assets
```pwsh
$ \tools\InsertionsClient.exe -d:\repos\Assets\default.config -m:\repos\Assets\manifest.json -i:\repos\Assets\ignored.txt
```
#### Restricting the affected packages with a whitelist
Location of additional needed resources...
1. _whitelist.txt_ located in \repos\Assets. Each line should be a regex pattern matching package IDs such as:  
`^VS\.Redist\.Common\.NetCore\.SharedFramework\.(x86|x64)\.[0-9]+\.[0-9]+$`
```pwsh
$ \tools\InsertionsClient.exe -d:\repos\Assets\default.config -m:\repos\Assets\manifest.json -wl:\repos\Assets\whitelist.txt
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

## Output
_InsertionsClient.exe_ outputs the results of running operations to both a persistent log file and to console.

**Successful NuGet version update** Every successfully updated NuGet version in _default.config_ has a corresponding message such as:
<pre>
12-3-2020 11:59:16.694366|thread:6|Package VS.Redist.Common.WindowsDesktop.SharedFramework.x64.3.1 was updated to version 3.1.2-servicing.20067.4
</pre>

**Completion statistics** update duration statistics are summarized
<pre>
Statistics:

ExactMatch - Update for case of matching manifest.json assets with multiple verions; where version of the exact matching NuGet asset was selected
No. Items: 31
Average: 41.4194-ms
Minimum: 5.0000-ms
Maximum: 269.0000-ms

NoMatch - No matching manifest.json assets for a given default.config NuGet
No. Items: 528
Average: 42.9811-ms
Minimum: 3.0000-ms
Maximum: 10,612.0000-ms
</pre>


**Completion Summary** Upon completion, _InsertionsClient.exe_ summarizes the duration & updated NuGet versions, as follows...
<pre>
Duration: 480.00-ms.
Successful updates: 27.
Updated default.config NuGet package versions...
        VS.Redist.Common.AspNetCore.SharedFramework.x64.3.1, version: 3.1.2-servicing.20068.1
        VS.Redist.Common.AspNetCore.SharedFramework.x86.3.1, version: 3.1.2-servicing.20068.1
        ...
</pre>