
namespace BuildScripts;

public class BuildContext : FrostingContext
{
    public string ArtifactsDir { get; }

    public PackContext PackContext { get; }

    public bool IsUniversalBinary { get; }

    public bool ShouldSkipTest { get; set; }

    public BuildContext(ICakeContext context) : base(context)
    {
        ArtifactsDir = context.Argument("artifactsDir", "artifacts");
        IsUniversalBinary = context.Argument("universalBinary", false);
        PackContext = new PackContext(context);

        if (context.BuildSystem().IsRunningOnGitHubActions &&
            !string.IsNullOrEmpty(context.EnvironmentVariable("GITHUB_TOKEN")))
        {
            context.BuildSystem().GitHubActions.Commands.SetSecret(context.EnvironmentVariable("GITHUB_TOKEN"));
        }
    }
}