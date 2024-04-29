namespace BuildScripts;

public static class FrostingContextExtensions
{
    private static readonly ProcessSettings _processSettings = new();

    public static void SetShellWorkingDir(this FrostingContext context, string path) => _processSettings.WorkingDirectory = path;

    public static int ShellExecute(this FrostingContext context, string command, string environmentVariables = "")
    {
        string shellCommandPath = context switch
        {
            _ when context.IsRunningOnWindows() => @"C:\msys64\usr\bin\bash.exe",
            _ when context.IsRunningOnLinux() => "sh",
            _ when context.IsRunningOnMacOs() => "zsh",
            _ => throw new PlatformNotSupportedException("Unsupported Platform")
        };

        _processSettings.Arguments = $"-c \"{environmentVariables} {command}\"";
        return context.StartProcess(shellCommandPath, _processSettings);
    }
}