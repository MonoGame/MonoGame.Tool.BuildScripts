using Cake.Common.Tools.VSWhere.Latest;

namespace BuildScripts;

[TaskName("Test Windows")]
public sealed class TestWindowsTask : FrostingTask<BuildContext>
{
    private static readonly string[] ValidLibs = {
        "WS2_32.dll",
        "KERNEL32.dll",
        "USER32.dll",
        "GDI32.dll",
        "WINMM.dll",
        "IMM32.dll",
        "ole32.dll",
        "OLEAUT32.dll",
        "VERSION.dll",
        "ADVAPI32.dll",
        "SETUPAPI.dll",
        "SHELL32.dll",
        "SHLWAPI.dll",
        "AVICAP32.dll",
        "bcrypt.dll",
        "msvcrt.dll"
    };

    public override bool ShouldRun(BuildContext context) => context.IsRunningOnWindows() && !context.ShouldSkipTest;

    public override void Run(BuildContext context)
    {
        var vswhere = new VSWhereLatest(context.FileSystem, context.Environment, context.ProcessRunner, context.Tools);
        var devcmdPath = vswhere.Latest(new VSWhereLatestSettings()).FullPath + @"\Common7\Tools\vsdevcmd.bat";


        // Ensure there are files to test otherwise this will always pass
        var files = Directory.GetFiles(context.ArtifactsDir);
        if (files is null || files.Length == 0)
        {
            throw new Exception("There are no files in the artifacts directory to test");
        }

        foreach (var filePath in files)
        {
            context.Information($"Checking: {filePath}");
            context.StartProcess(
                devcmdPath,
                new ProcessSettings()
                {
                    Arguments = $"& dumpbin /dependents /nologo \"{filePath}\"",
                    RedirectStandardOutput = true
                },
                out IEnumerable<string> processOutput
            );

            var passedTests = true;
            foreach (string output in processOutput)
            {
                var libPath = output.Trim();
                if (!libPath.EndsWith(".dll") || libPath.Contains(' '))
                    continue;

                if (ValidLibs.Contains(libPath))
                {
                    context.Information($"VALID: {libPath}");
                }
                else
                {
                    context.Information($"INVALID: {libPath}");
                    passedTests = false;
                }
            }

            if (!passedTests)
            {
                throw new Exception("Invalid library linkage detected!");
            }
        }
    }
}