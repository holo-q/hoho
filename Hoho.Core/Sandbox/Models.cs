namespace Hoho.Core.Sandbox;

public enum SandboxMode
{
    ReadOnly,
    WorkspaceWrite,
    DangerFullAccess
}

public enum ApprovalPolicy
{
    Untrusted,
    OnFailure,
    OnRequest,
    Never
}

public sealed record SandboxSettings
{
    public SandboxMode Mode { get; init; } = SandboxMode.WorkspaceWrite;
    public ApprovalPolicy Approval { get; init; } = ApprovalPolicy.OnFailure;
    public bool NetworkEnabledInWorkspaceWrite { get; init; }
}

