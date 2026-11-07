using Ecosys.Windows.UI;

ApplicationConfiguration.Initialize();

Application.ThreadException += (_, args) => Report(args.Exception);

AppDomain.CurrentDomain.UnhandledException += (_, args) =>
{
    if (args.ExceptionObject is Exception exception)
        Report(exception);
};

Application.Run(new EcosysForm());

static void Report(Exception exception)
{
    try
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ecosys");
        Directory.CreateDirectory(folder);
        File.AppendAllText(
            Path.Combine(folder, "crash.log"),
            $"[{DateTime.Now:O}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
    }
    catch
    {
        // Logging must never crash the app.
    }

    MessageBox.Show(
        exception.Message,
        "Ecosys",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);
}
