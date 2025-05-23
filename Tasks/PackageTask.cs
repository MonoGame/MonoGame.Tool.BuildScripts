using System.Runtime.InteropServices;
using Cake.Common.Tools.DotNet.NuGet.Push;

namespace BuildScripts;

[TaskName("Package")]
public sealed class PackageTask : AsyncFrostingTask<BuildContext>
{

    public override async Task RunAsync(BuildContext context)
    {
        // Create a temporary directory tha we can use to build the "project" in that we'll pack into a dotnet tool
        var projectDir = new DirectoryPath("temp");
        context.CreateDirectory(projectDir);

        // If this is running on a github runner, then download the remote artifacts from github, otherwise, use the
        // local artifacts so we can test/run this locally as well
        if (context.BuildSystem().IsRunningOnGitHubActions)
        {
            string[] requiredRids = context.IsUniversalBinary ?
                ["windows-x64", "linux-x64", "osx"] :
                ["windows-x64", "linux-x64", "osx-x64", "osx-arm64"];

            foreach (var rid in requiredRids)
            {
                var directoryPath = new DirectoryPath($"{projectDir}/binaries/{rid}");
                if (context.DirectoryExists(directoryPath))
                    continue;

                context.CreateDirectory(directoryPath);
                await context.BuildSystem().GitHubActions.Commands.DownloadArtifact($"artifacts-{rid}", directoryPath);
            }
        }
        else
        {
            string rid = string.Empty;
            if (context.IsRunningOnWindows())
            {
                rid = "windows-x64";
            }
            else if (context.IsRunningOnLinux())
            {
                rid = "linux-x64";
            }
            else if (context.IsRunningOnMacOs())
            {
                if (context.IsUniversalBinary) rid = "osx";
                else rid = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.Arm or Architecture.Arm64 => "osx-arm64",
                    _ => "osx-x64"
                };
            }

            var copyToDir = new DirectoryPath($"{projectDir}/binaries/{rid}");
            context.CopyDirectory($"{context.ArtifactsDir}/artifacts-{rid}", copyToDir);
        }

        // Create the temporary project that we'll use to pack into the dotnet tool
        var projectPath = $"{projectDir}/MonoGame.Tool.{context.PackContext.ToolName}.csproj";
        await WriteEmbeddedResource(context, "MonoGame.Tool.X.txt", projectPath);
        await WriteEmbeddedResource(context, "MonoGame.Tool.X.targets", $"{projectDir}/MonoGame.Tool.{context.PackContext.ToolName}.targets");
        await WriteEmbeddedResource(context, "Program.txt", $"{projectDir}/Program.cs");

        var readMeName = "README.md";
        var readMePath = $"{projectDir}/{readMeName}";
        await File.WriteAllTextAsync(readMePath, context.PackContext.Description);

        await SaveEmbeddedResourceAsync("Icon.png", $"{projectDir}/Icon.png");

        // Pack the project into a dotnet tool
        var dnMsBuildSettings = new DotNetMSBuildSettings();
        dnMsBuildSettings.WithProperty("Version", context.PackContext.Version);
        dnMsBuildSettings.WithProperty("RepositoryUrl", context.PackContext.RepositoryUrl);

        context.DotNetPack(projectPath, new DotNetPackSettings()
        {
            MSBuildSettings = dnMsBuildSettings,
            Verbosity = DotNetVerbosity.Minimal,
            Configuration = "Release",
            OutputDirectory = context.ArtifactsDir
        });

        // When running on a github runner, upload the dotnet tool nupkg to github otherwise just copy it to the
        // artifacts directory for local testing.
        if (context.BuildSystem().IsRunningOnGitHubActions)
        {
            foreach (var nugetPath in context.GetFiles($"{context.ArtifactsDir}/**/*.nupkg"))
            {
                await context.BuildSystem().GitHubActions.Commands.UploadArtifact(nugetPath, nugetPath.GetFilename().ToString());

                if (context.PackContext.IsTag)
                {
                    context.DotNetNuGetPush(nugetPath, new DotNetNuGetPushSettings()
                    {
                        ApiKey = context.EnvironmentVariable("GITHUB_TOKEN"),
                        Source = $"https://nuget.pkg.github.com/{context.PackContext.RepositoryOwner}/index.json"
                    });
                }
            }
        }

        // Clean up the temp folder now that we're done
        context.DeleteDirectory(projectDir, new() { Force = true, Recursive = true });
    }

    private static async Task RunOnGithubAsync(BuildContext context, string projectDir)
    {
        // Download remote artifacts from github
        string[] requiredRids = context.IsUniversalBinary ?
            ["windows-x64", "linux-x64", "osx" ]:
            ["windows-x64", "linux-x64", "osx-x64", "osx-arm64"];

        foreach (var rid in requiredRids)
        {
            var directoryPath = new DirectoryPath($"{projectDir}/binaries/{rid}");
            if (context.DirectoryExists(directoryPath))
                continue;

            context.CreateDirectory(directoryPath);
            await context.BuildSystem().GitHubActions.Commands.DownloadArtifact($"artifacts-{rid}", directoryPath);
        }
    }

    private static async Task WriteEmbeddedResource(BuildContext context, string resource, string outputPath)
    {
        var licenseName = System.IO.Path.GetExtension(context.PackContext.LicensePath) switch
        {
            ".txt" => "LICENSE.txt",
            ".md" => "LICENSE.md",
            _ => "LICENSE"
        };
        var contentInclude = $"<None Include=\"binaries\\**\\*\" CopyToOutputDirectory=\"PreserveNewest\" />";
        await using var stream = typeof(PackageTask).Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        var outputData = (await reader.ReadToEndAsync())
            .Replace("{X}", context.PackContext.ToolName)
            .Replace("{x}", context.PackContext.ToolName.ToLower())
            .Replace("{ExecutableName}", context.PackContext.ExecutableName)
            .Replace("{Description}", context.PackContext.Description)
            .Replace("{LicensePath}", context.PackContext.LicensePath)
            .Replace("{LicenseName}", licenseName)
            .Replace("{ContentInclude}", contentInclude);
        await File.WriteAllTextAsync(outputPath, outputData);
    }

    private static async Task SaveEmbeddedResourceAsync(string resourceName, string outPath)
    {
        if (File.Exists(outPath))
            File.Delete(outPath);

        await using var stream = typeof(PackageTask).Assembly.GetManifestResourceStream(resourceName)!;
        await using var writer = File.Create(outPath);
        await stream.CopyToAsync(writer);
        writer.Close();
    }
}