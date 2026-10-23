using System.Collections.Generic;
using Emberfall.Core.Session;

// The test store's flag (R26): EMBERFALL_TESTSTORE turns TestStoreSwitch.Enabled on; build-check asserts both cases.
internal static partial class Probe
{
    static partial void Extra(List<string> lines)
    {
        lines.Add("TestStoreSwitch.Enabled=" + TestStoreSwitch.Enabled);
    }
}
