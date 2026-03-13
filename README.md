## TcHmiLogixDriver

A Rockwell Automation communcation driver for TwinCAT HMI.

Uses the [LogixDriver](https://github.com/bstiffler582/LogixDriver) wrapper for [libplctag.NET](https://github.com/libplctag/libplctag.NET/) to communicate via EtherNet/IP to ControlLogix platform PLCs.

Within the TwinCAT HMI framework, this driver supports:
- Connection state monitoring / reconnection
- Tag browsing
- Automatic type and value resolution