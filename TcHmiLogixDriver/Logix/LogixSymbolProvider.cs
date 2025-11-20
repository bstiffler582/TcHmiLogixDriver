using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using TcHmiSrv.Core;
using TcHmiSrv.Core.Tools.DynamicSymbols;
using TcHmiSrv.Core.Tools.Resolving.Handlers;

namespace TcHmiLogixDriver
{
    public class LogixSymbolProvider : DynamicSymbolsProvider
    {
        private HashSet<string> mapped = new HashSet<string>();
        public override IEnumerable<Command> HandleCommands(CommandGroup commands)
        {
            foreach (var command in commands)
            {
                switch (command.Mapping)
                {
                    case "ListSymbols":
                        break;
                    case "GetSchema":
                        // cache requested schemas (mapped symbols)
                        var formatted = (string)command.WriteValue;
                        mapped.Add(formatted.Substring(formatted.IndexOf("::") + 2));
                        break;
                    default:
                        if (!mapped.TryGetValue(command.Path, out var match))
                        {
                            match = mapped.FirstOrDefault(m => command.Path.StartsWith(m));
                            if (match != null)
                            {
                                var cmd = commands.FirstOrDefault(command);
                                cmd.Name = match;
                                cmd.Changed = true;
                            }
                        }
                        break;
                }
            }

            return base.HandleCommands(commands);
        }
    }
}