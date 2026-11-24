using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        string appPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "bin",
            "SpeakerAnimator.exe"
        );

        if (!File.Exists(appPath))
        {
            MessageBox.Show(
                "Application not found:\n" + appPath,
                "Speaker Animator Launcher",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = appPath,
                WorkingDirectory = Path.GetDirectoryName(appPath),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Speaker Animator Launcher");
        }
    }
}