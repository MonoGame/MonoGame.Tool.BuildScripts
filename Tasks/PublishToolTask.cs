using System.Runtime.InteropServices;

namespace BuildScripts;

[TaskName("Publish Tool")]
[IsDependentOn(typeof(PrepTask))]
public sealed class PublishToolTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        var rid = "";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            rid = "windows";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            rid = "osx";
        else
            rid = "linux";

        if (!(RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && context.IsUniversalBinary))
        {
            rid += RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm or Architecture.Arm64 => "-arm64",
                _ => "-x64",
            };
        }

        var copyTo = $"artifacts-{rid}";
        if (context.BuildSystem().IsRunningOnGitHubActions)
        {
            await context.BuildSystem().GitHubActions.Commands.UploadArtifact(DirectoryPath.FromString(context.ArtifactsDir), copyTo);
        }
        else
        {
            //  When running locally, make the artifacts directory mimic what github would look like
            var files = Directory.GetFiles(context.ArtifactsDir);
            context.CreateDirectory(new DirectoryPath($"{context.ArtifactsDir}/{copyTo}"));
            foreach (var file in files)
            {
                context.MoveFileToDirectory(file, new DirectoryPath($"{context.ArtifactsDir}/{copyTo}"));
            }
        }
    }
}