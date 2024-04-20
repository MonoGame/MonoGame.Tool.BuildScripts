
namespace BuildScripts;

public class PackContext
{
    public string ToolName { get; }

    public string CommandName { get; }

    public string LicensePath { get; }

    public string? RepositoryOwner { get; }

    public string? RepositoryUrl { get; }

    public string Version { get; }

    public bool IsTag { get; }

    public PackContext(ICakeContext context)
    {
        ToolName = context.Arguments("libraryname", "X").FirstOrDefault()!;
        CommandName = context.Arguments("commandname", "X").FirstOrDefault()!;
        LicensePath = context.Arguments("licensepath", "").FirstOrDefault()!;
        Version = "1.0.0";
        IsTag = false;

        if (context.BuildSystem().IsRunningOnGitHubActions)
        {
            RepositoryOwner = context.EnvironmentVariable("GITHUB_REPOSITORY_OWNER");
            RepositoryUrl = $"https://github.com/{context.EnvironmentVariable("GITHUB_REPOSITORY")}";
            IsTag = context.EnvironmentVariable("GITHUB_REF_TYPE") == "tag";

            if (IsTag)
            {
                Version = context.EnvironmentVariable("GITHUB_REF_NAME");
            }
        }
    }
}