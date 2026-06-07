namespace Atom;

public interface IPullRequestHelper : IBuildAccessor
{
    [ParamDefinition("pull-request-number", "The pull request number to approve.")]
    int PullRequestNumber => GetParam(() => PullRequestNumber);
}
