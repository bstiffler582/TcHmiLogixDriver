using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix.Proto
{
    public interface ITagDefinitionExpander
    {
        TagDefinition ExpandTagDefinition(TagDefinition root, bool deep = true);
    }
}
