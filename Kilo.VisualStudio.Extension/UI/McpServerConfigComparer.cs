using System;
using System.Collections.Generic;
using Kilo.VisualStudio.App.Services;

namespace Kilo.VisualStudio.Extension.UI
{
    internal class McpServerConfigComparer : IEqualityComparer<McpServerConfig>
    {
        public bool Equals(McpServerConfig? x, McpServerConfig? y)
        {
            if (x == null || y == null)
                return false;

            return string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(McpServerConfig obj)
        {
            return obj?.Id?.ToLowerInvariant().GetHashCode() ?? 0;
        }
    }
}
