# Overview
**InsertionsClient** updates the versions of NuGet packages in _default.config_ with the corresponding versions specified in _manifest.json_ assets.

## How does **InsertionsClient** work?
1. Loads into memory the contents of both _default.config_ and _manifest.json_
1. Searches _default.config_ for corresponding NuGet packages for each _manifest.json_ asset
1. For every match, the NuGet version in _default.config_ is updated with that of the corresponding _manifest.json_ asset
1. The updated _default.config_ is saved on disk

## Input
1. **-d:** Path to the default.config.  Example: _-d:[c:\default.config]_
1. **-m:** Path to the manifest.json.  Example: _-m:[c:\files\manifest.json]_
1. **-w:** Maximum allowed duration in seconds [**optional**].  Example: _-w:60_
1. **-c:** Maximum concurrency of default.config version updates [**optional**].  Example: _-c:10_

_Warnings_
1. NO SPACES ALLOWED IN EITHER default.config OR manifest.json FILE PATHS
1. The default duration & concurrency values should suffice

## Log
* **InsertionsClient** creates a log detailing every step taken
* The logs are placed in the **Logs** folder relative to the location of the _InsertionsClient.exe_
### Log Format
Each log line details...
1. The time stamp when the message was logged
1. the id of the thread where the message was logged
1. the message logged
### Sample Log Lines
<pre>
16-2-2020 09:03:43.964561 (thread:1) Set max wait seconds to 75
16-2-2020 09:03:43.966524 (thread:1) Set max concurrency to 10
16-2-2020 09:03:44.016178 (thread:1) Loading default.config content onto memory from C:\Users\joaguila\source\repos\DeafultConfigClient\tests\DefaultConfigClientTest\Assets\default.config.
16-2-2020 09:03:44.026415 (thread:1) Loaded default.config content onto memory.
16-2-2020 09:03:44.466310 (thread:1) De-serialized 19 builds from manifest.json.
</pre>

## Use
1. Launch command line session
1. Navigate to the location of _InsertionsClient.exe_
1. Alternative, if on WINDOWS, set **InsertionsClient** on the %path% variable to run the application from any location
1. Launch _InsertionsClient.exe_ with the proper parameters
### Example
The example below details staring the application for the following conditions...
1. _InsertionsClient.exe_ located on \tools
1. _default.config_ located in \repos\Assets
1. _manifest.json_ located in \repos\Assets
<pre>
$ \tools\InsertionsClient.exe -d:\repos\Assets\default.config -m:\repos\Assets\manifest.json
</pre>

## Output
The output of _InsertionsClient.exe_ runs is both persisted on console and log.
**Successful NuGet vesrion update** Every successfully updated NuGet version in _default.config_ has a corresponding message such as:
<pre>
16-2-2020 09:04:32.696289 (thread:10) Succeeded to update VS.Redist.Common.NetCore.HostFXR.x64.3.1.
</pre>

**Failed NuGet lookups** _manifest.json_ assets without matching NuGet packages in _default.config_ result in messages such as:
<pre>
16-2-2020 09:04:32.717231 (thread:1) Failed to update assets/symbols/runtime.linux-arm.Microsoft.NETCore.DotNetAppHost.3.1.2.symbols.nupkg.  Reason: Sequence contains no matching element.
</pre>

**Completion statistics** update duration statistics are summarized
<pre>
16-2-2020 02:16:57.236372 (thread:1) Statistics
Successful Outcomes: No. Items: 28
Average: 0.1429-ms
Minimum: 0.0000-ms
Maximum: 1.0000-ms

Failed Outcomes: No. Items: 2138
Average: 1.3241-ms
Minimum: 0.0000-ms
Maximum: 44.0000-ms
</pre>


**Completion Summary** Upon completion, _InsertionsClient.exe_ summarizes the duration & updated NuGet versions, as follows...
<pre>
16-2-2020 02:16:57.249150 (thread:1) Duration: 46,321.00-ms.
16-2-2020 02:16:57.279854 (thread:1) Successful matches: 28.
16-2-2020 02:16:57.283320 (thread:1) Failed matches: 2,138.
</pre>