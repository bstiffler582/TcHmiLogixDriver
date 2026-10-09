## TcHmiLogixDriver

A Rockwell Automation communcation driver for TwinCAT HMI.

Uses the [LogixDriver](https://github.com/bstiffler582/LogixDriver) wrapper for [libplctag.NET](https://github.com/libplctag/libplctag.NET/) to communicate via EtherNet/IP to ControlLogix platform PLCs.

Within the TwinCAT HMI framework, this driver supports:
- Connection state monitoring / reconnection
- Tag browsing
- Automatic type and value resolution

### Installation
Download the latest `.nupkg` [release](https://github.com/bstiffler582/TcHmiLogixDriver/releases), then place it in a nuget offline packages folder (e.g. `C:\ProgramData\Beckhoff\NuGetPackages`). It will now be availabe to install in your TwinCAT HMI project via the nuget package manager GUI (`References -> Packages`) or CLI (`nuget install TcHmiLogixDriver`).

### Tag Mapping

The tag browser allows you to map top-level UDTs and arrays, *or* individual members / elements. **What you map decides what gets read from the PLC.** Any request that falls under a mapped path is served by one read of that mapped tag.

#### Example

For instance, take this tag model in the PLC:

```
Line                 LINE_UDT
├── Speed            REAL
├── Running          BOOL
└── Motors           MOTOR_UDT[4]
    └── [0..9]
        ├── Speed    REAL
        ├── Current  REAL
        └── Faults   DINT
```

with the following mapped symbols:

```
Line
Line.Motors
Line.Motors[2].Speed
```

To serve a binding, the driver walks down a mapping tree along the binding's path and stops at the deepest node it reaches. That node is the tag it reads from the PLC:

| Control Binding | Mapped path | Read from PLC |
|---|---|---|
| `Line.Speed` | `Line` | `Line` (whole structure) |
| `Line.Motors[0].Current` | `Line.Motors` | `Line.Motors` (whole array) |
| `Line.Motors[2].Speed` | `Line.Motors[2].Speed` | `Line.Motors[2].Speed` (single primitive) |

Notice that the binding to `Line::Motors::0::Current` causes the whole `Line` structure to be read!

>- **Map a parent structure or array** when the HMI shows many of its members. Every binding under it (`Line.Speed`, `Line.Running`, …) is served by a single PLC request.
>- **Map individual members** when you only need a few fields from a large structure or array, so the driver doesn't transfer the whole thing for each request.
>- When mappings overlap, **the most specific path wins**. `Line` is mapped, but `Line.Motors[2].Speed` has its own mapping, so bindings for that tag read just that member and not all of `Line`.

### Writes

Writes don't use the mapping tree. The requested path is turned straight into a tag name (`Line.Motors[0].Current` → `Line.Motors[0].Current`) and written as is. A structure write must include every member of the structure.

### Dependencies
- Beckhoff.TwinCAT.HMI.Server.Engineering >= 14.5.73.0 (22.0.7983)