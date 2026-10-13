namespace TableDb.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var form = new MainForm();
        if (args.Length > 0 && File.Exists(args[0]))
            form.Shown += (_, _) => form.OpenFile(args[0]);
        Application.Run(form);
    }
}
