
using System.Runtime.InteropServices;
using Cake.Common.Tools.DotNet.NuGet.Push;

namespace BuildScripts;

[TaskName("Package")]
public sealed class PublishPackageTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        var projectDir = CreateProjectDirectory(context);
        await DownloadArtifactsAsync(context, projectDir);
        var projectPath = await CreateProjectAsync(context, projectDir);
        PackProject(context, projectPath);
        await UploadArtifactsAsync(context, projectDir);
    }

    private static DirectoryPath CreateProjectDirectory(BuildContext context)
    {
        var projectName = $"MonoGame.Tool.{context.PackContext.ToolName}";
        var projectDir = new DirectoryPath($"{context.ArtifactsDir}/{projectName}");
        context.CreateDirectory(projectDir);
        return projectDir;
    }

    private static async Task DownloadArtifactsAsync(BuildContext context, DirectoryPath projectDir)
    {
        if (context.BuildSystem().IsRunningOnGitHubActions)
            await DownloadRemoteArtifactsAsync(context, projectDir);
        else
            DownloadLocalArtifacts(context, projectDir);
    }

    private static async Task DownloadRemoteArtifactsAsync(BuildContext context, DirectoryPath projectDir)
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

    private static void DownloadLocalArtifacts(BuildContext context, DirectoryPath projectDir)
    {
        string rid = string.Empty;
        if (context.IsRunningOnWindows()) rid = "windows-x64";
        else if (context.IsRunningOnLinux()) rid = "linux-x64";
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
        context.CopyDirectory(context.ArtifactsDir, copyToDir);
    }

    private static async Task<string> CreateProjectAsync(BuildContext context, DirectoryPath projectDir)
    {
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

        string projectPath = $"{projectDir}/{projectDir}.csproj";
        await File.WriteAllTextAsync(projectPath, projectData);

        var programData = await ReadEmbeddedResourceAsync("Program.txt");
        programData = programData.Replace("{ExecutableName}", context.PackContext.ExecutableName);
        var programPath = $"{projectDir}/Program.cs";
        await File.WriteAllTextAsync(programPath, programData);

        await SaveEmbeddedResourceAsync("Icon.png", $"{projectDir}/Icon.png");

        return projectPath;
    }

    private static void PackProject(BuildContext context, string projectPath)
    {
        var dnMsBuildSettings = new DotNetMSBuildSettings();
        dnMsBuildSettings.WithProperty("Version", context.PackContext.Version);
        dnMsBuildSettings.WithProperty("RepositoryUrl", context.PackContext.RepositoryUrl);

        context.DotNetPack(projectPath, new DotNetPackSettings()
        {
            MSBuildSettings = dnMsBuildSettings,
            Verbosity = DotNetVerbosity.Minimal,
            Configuration = "Release"
        });
    }

    private static async Task UploadArtifactsAsync(BuildContext context, DirectoryPath projectDir)
    {
        if(context.BuildSystem().IsRunningOnGitHubActions)
            await UploadRemoteArtifactsAsync(context, projectDir);
        else
            UploadLocalArtifact(context, projectDir);
    }

    private static async Task UploadRemoteArtifactsAsync(BuildContext context, DirectoryPath projectDir)
    {
        foreach (var nugetPath in context.GetFiles($"{projectDir}/bin/Release/*.nupkg"))
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

    private static void UploadLocalArtifact(BuildContext context, DirectoryPath projectDir)
    {
        foreach (var nugetPath in context.GetFiles($"{projectDir}/bin/Release/*.nupkg"))
        {
            var fileName = nugetPath.GetFilename();
            var copyTo = new FilePath($"{projectDir}/{fileName}");
            context.CopyFile(nugetPath, copyTo);
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