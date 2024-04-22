
using System.Runtime.InteropServices;
using Cake.Common.Tools.DotNet.NuGet.Push;

namespace BuildScripts;

[TaskName("Package")]
public sealed class PublishPackageTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        // Create a temporary directory tha we can use to build the "project" in that we'll pack into a dotnet tool
        var projectDir = new DirectoryPath("temp");
        context.CreateDirectory(projectDir);

        //  If this is running on a github runner, then download the remote artifacts from github, otherwise, use the
        //  local artifacts so we can test/run this locally as well
        if (context.BuildSystem().IsRunningOnGitHubActions)
        {
            var requiredRids = context.IsUniversalBinary ?
                new string[] { "windows-x64", "linux-x64", "osx" } :
                new string[] { "windows-x64", "linux-x64", "osx-x64", "osx-arm64" };

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
        var licensePath = context.PackContext.LicensePath;
        var licenseName = "LICENSE";

        if (licensePath.EndsWith(".txt")) licenseName += ".txt";
        else if (licensePath.EndsWith(".md")) licenseName += ".md";

        var contentInclude = $"<Content Include=\"binaries\\**\\*\" CopyToOutputDirectory=\"PreserveNewest\" />";

        var projectData = await ReadEmbeddedResourceAsync("MonoGame.Tool.X.txt");
        projectData = projectData.Replace("{X}", context.PackContext.ToolName)
                                 .Replace("{CommandName}", context.PackContext.CommandName)
                                 .Replace("{LicensePath}", context.PackContext.LicensePath)
                                 .Replace("{LicenseName}", licenseName)
                                 .Replace("{ContentInclude}", contentInclude);

        string projectPath = $"{projectDir}/MonoGame.Tool.{context.PackContext.ToolName}.csproj";
        await File.WriteAllTextAsync(projectPath, projectData);

        var programData = await ReadEmbeddedResourceAsync("Program.txt");
        programData = programData.Replace("{ExecutableName}", context.PackContext.ExecutableName);
        var programPath = $"{projectDir}/Program.cs";
        await File.WriteAllTextAsync(programPath, programData);

        await SaveEmbeddedResourceAsync("Icon.png", $"{projectDir}/Icon.png");

        //  Pack the project into a dotnet tool
        var dnMsBuildSettings = new DotNetMSBuildSettings();
        dnMsBuildSettings.WithProperty("Version", context.PackContext.Version);
        dnMsBuildSettings.WithProperty("RepositoryUrl", context.PackContext.RepositoryUrl);

        context.DotNetPack(projectPath, new DotNetPackSettings()
        {
            MSBuildSettings = dnMsBuildSettings,
            Verbosity = DotNetVerbosity.Minimal,
            Configuration = "Release"
        });

        // When running on a github runner, upload the dotnet tool nupkg to github otherwise just copy it to the
        // artifacts directory for local testing.
        if (context.BuildSystem().IsRunningOnGitHubActions)
        {
            foreach (var nugetPath in context.GetFiles($"{projectDir}/**/*.nupkg"))
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
        else
        {
            context.CopyFiles($"{projectDir}/**/*.nupkg", context.ArtifactsDir);
        }

        //  Clean up the temp folder now that we're done
        context.DeleteDirectory(projectDir, new() { Force = true, Recursive = true });
    }

    private static async Task RunOnGithubAsync(BuildContext context, string projectDir)
    {
        // Download remote artifacts from github
        var requiredRids = context.IsUniversalBinary ?
            new string[] { "windows-x64", "linux-x64", "osx" } :
            new string[] { "windows-x64", "linux-x64", "osx-x64", "osx-arm64" };

        foreach (var rid in requiredRids)
        {
            var directoryPath = new DirectoryPath($"{projectDir}/binaries/{rid}");
            if (context.DirectoryExists(directoryPath))
                continue;

            context.CreateDirectory(directoryPath);
            await context.BuildSystem().GitHubActions.Commands.DownloadArtifact($"artifacts-{rid}", directoryPath);
        }
    }

    private static async Task<string> ReadEmbeddedResourceAsync(string resourceName)
    {
        await using var stream = typeof(PublishPackageTask).Assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static async Task SaveEmbeddedResourceAsync(string resourceName, string outPath)
    {
        if (File.Exists(outPath))
            File.Delete(outPath);

        await using var stream = typeof(PublishPackageTask).Assembly.GetManifestResourceStream(resourceName)!;
        await using var writer = File.Create(outPath);
        await stream.CopyToAsync(writer);
        writer.Close();
    }
}