using System;
using System.Collections.Generic;
using Emberfall.Core.Session;

// DEFINE PROBE (R26): prints what the build's scripting defines do to the switches that ship in Core.
// build-check compiles this with the Core files once with no defines (the shipped build) and once with
// the opt-out and test defines, runs both, and asserts the printed lines. One line per flag, "name=value".
//
// A switch owner adds a line without editing this file: write a partial class Probe in its own file
// under Tools/DefineProbe and implement Extra (for example the test store's flag).
internal static partial class Probe
{
    private static int Main()
    {
        List<string> lines = new List<string>();
        lines.Add("UiKitSwitch.Enabled=" + UiKitSwitch.Enabled);
        lines.Add("UiKitSwitch.ShippedDefault=" + UiKitSwitch.ShippedDefault);
        Extra(lines);
        for (int i = 0; i < lines.Count; i++) Console.WriteLine(lines[i]);
        return 0;
    }

    static partial void Extra(List<string> lines);
}
