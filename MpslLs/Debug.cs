class Debug
{
    const string LOG_FILE = "lsp.log";

    public static bool DebugMode { get; set; }

    public static void Initialize()
    {
        File.Delete(LOG_FILE);
    }

    public static void Log(object? value)
    {
        if (DebugMode)
        {
            File.AppendAllText(LOG_FILE, $"{value}\n");
        }
    }
}