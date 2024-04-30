namespace BuildScripts;

public static class FrostingContextExtensions
{
    private static readonly ProcessSettings _processSettings = new();
    private static string _environmentVariables = string.Empty;

    public static void SetShellWorkingDir(this FrostingContext context, string path) => _processSettings.WorkingDirectory = path;

    public static void SetShellEnvironmentVariables(this FrostingContext context, params string[] env)
    {
        _environmentVariables = string.Empty;
        for(int i = 0; i < env.Length; i+= 2)
        {
            string key = env[i];
            string value = $"'{env[i + 1]}'";
            _environmentVariables += $"export {key}={value};"; 
        }
    }

    public static void ClearShellEnvironmentVariables(this FrostingContext context) => _environmentVariables = string.Empty;

    public static int ShellExecute(this FrostingContext context, string command)
    {
        string shellCommandPath = context switch
        {
            _ when context.IsRunningOnWindows() => @"C:\msys64\usr\bin\bash.exe",
            _ when context.IsRunningOnLinux() => "sh",
            _ when context.IsRunningOnMacOs() => "zsh",
            _ => throw new PlatformNotSupportedException("Unsupported Platform")
        };

        _processSettings.Arguments = $"-c \"{_environmentVariables} {command}\"";
        context.Information($"Executing: {shellCommandPath} {string.Join(' ', _processSettings.Arguments)}");
        return context.StartProcess(shellCommandPath, _processSettings);
    }
}