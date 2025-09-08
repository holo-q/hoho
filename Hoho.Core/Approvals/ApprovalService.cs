using Hoho.Core.Sandbox;

namespace Hoho.Core.Approvals;

public sealed class ApprovalService
{
    private readonly ApprovalPolicy _policy;
    public ApprovalService(ApprovalPolicy policy)
    {
        _policy = policy;
    }

    public bool Confirm(string actionDescription)
    {
        switch (_policy)
        {
            case ApprovalPolicy.Never:
                return true;
            case ApprovalPolicy.OnFailure:
                // Allow upfront; caller retries on failure if needed
                return true;
            case ApprovalPolicy.OnRequest:
            case ApprovalPolicy.Untrusted:
                return Ask($"Proceed: {actionDescription}? [y/N] ");
            default:
                return false;
        }
    }

    private static bool Ask(string prompt)
    {
        Console.Write(prompt);
        var ans = Console.ReadLine();
        return ans is not null && (ans.Equals("y", StringComparison.OrdinalIgnoreCase) || ans.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }
}

