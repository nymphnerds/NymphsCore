using System.Windows.Forms;

namespace LlamaServerMonitor;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Ensure only one instance runs
        using var mutex = new System.Threading.Mutex(true, "Global\\LlamaServerMonitor_Mutex", out var createdNew);
        if (!createdNew)
        {
            // Another instance is already running
            return;
        }

        Application.Run(new MainForm());
    }
}