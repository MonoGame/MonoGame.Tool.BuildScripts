
namespace BuildScripts;

[TaskName("Test Linux")]
public sealed class TestLinuxTask : FrostingTask<BuildContext>
{
    private static readonly string[] LibPrefix = {
        "linux-vdso.so",
        "libstdc++.so",
        "libgcc_s.so",
        "libc.so",
        "libm.so",
        "libdl.so",
        "libmvec.so",
        "libpthread.so",
        "/lib/ld-linux-",
        "/lib64/ld-linux-"
    };

    public override bool ShouldRun(BuildContext context) => context.IsRunningOnLinux() && !context.ShouldSkipTest;

    public override void Run(BuildContext context)
    {
        List<string> libSufix = [];
        GetValidSufix(context, context.ArtifactsDir, libSufix);
        
        if (libSufix.Count == 0)
        {
            throw new Exception("There are no files in the artifacts directory to test");
        }

        CheckDir(context, context.ArtifactsDir, libSufix);
    }

    private void GetValidSufix(BuildContext context, string dir, List<string> libSufix)
    {
        foreach (var dirPath in Directory.GetDirectories(dir))
        {
            GetValidSufix(context, dirPath, libSufix);
        }

        foreach (var filePath in Directory.GetFiles(dir))
        {
            libSufix.Add(System.IO.Path.GetFileName(filePath));
        }
    }

    private void CheckDir(BuildContext context, string dir, List<string> libSufix)
    {
        foreach (var dirPath in Directory.GetDirectories(dir))
        {
            CheckDir(context, dirPath, libSufix);
        }

        foreach (var filePath in Directory.GetFiles(dir))
        {
            context.Information($"Checking: {filePath}");
            context.StartProcess(
                "ldd",
                new ProcessSettings
                {
                    Arguments = $"\"{filePath}\"",
                    RedirectStandardOutput = true
                },
                out IEnumerable<string> processOutput
            );

            var passedTests = true;
            foreach (var line in processOutput)
            {
                var libPath = line.Trim().Split(' ')[0];

                var isValidLib = false;
                foreach (var validPrefix in LibPrefix)
                {
                    if (libPath.StartsWith(validPrefix))
                    {
                        isValidLib = true;
                        break;
                    }
                }
                foreach (var validSufix in libSufix)
                {
                    if (libPath.EndsWith(validSufix))
                    {
                        isValidLib = true;
                        break;
                    }
                }

                if (isValidLib)
                    {
                        context.Information($"VALID: {libPath}");
                    }
                    else
                    {
                        var pathToCheck = System.IO.Path.Combine(context.ArtifactsDir, libPath);
                        if (!libPath.Contains('/') && File.Exists(pathToCheck))
                        {
                            context.Information($"VALID linkage: {libPath}");
                        }
                        else
                        {
                            context.Information($"INVALID linkage: {libPath}");
                            passedTests = false;
                        }
                    }
            }

            if (!passedTests)
            {
                throw new Exception("Invalid library linkage detected!");
            }

            context.Information("");
        }
    }
}