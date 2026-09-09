using System;
using System.Windows.Forms;

namespace UTMMonitor;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var form = new MainForm();
        UpdateUi.Install(form);
        MarkBatchUi.Install(form);
        EgaisControlUi.Install(form);
        Application.Run(form);
    }
}
