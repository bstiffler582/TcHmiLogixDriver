## TcHmiLogixDriver

A Rockwell Automation communcation driver for TwinCAT HMI.

Uses the [LogixDriver](https://github.com/bstiffler582/LogixDriver) wrapper for [libplctag.NET](https://github.com/libplctag/libplctag.NET/) to communicate via EtherNet/IP to ControlLogix platform PLCs.

Within the TwinCAT HMI framework, this driver supports:
- Connection state monitoring / reconnection
- Tag browsing
- Automatic type and value resolution

### Installation
Download the latest version `.nupkg` from the [release](/release) folder, then place it in a nuget offline packages folder (e.g. `C:\ProgramData\Beckhoff\NuGetPackages`). It will now be availabe to install in your TwinCAT HMI project via the nuget package manager GUI (`References -> Packages`) or CLI (`nuget install TcHmiLogixDriver`).

### Dependencies
- Beckhoff.TwinCAT.HMI.Server.Engineering >= 14.5.73.0 (22.0.7983)