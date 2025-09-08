namespace Hoho.Core.Planning;

public enum PlanStatus { Pending, InProgress, Completed }

public sealed record PlanStep(string Step, PlanStatus Status);

public sealed class Plan
{
    private readonly List<PlanStep> _steps = new();
    public IReadOnlyList<PlanStep> Steps => _steps;

    public void SetSteps(IEnumerable<PlanStep> steps)
    {
        _steps.Clear();
        _steps.AddRange(steps);
        EnsureSingleInProgress();
    }

    public void UpdateStep(int index, PlanStep updated)
    {
        _steps[index] = updated;
        EnsureSingleInProgress();
    }

    private void EnsureSingleInProgress()
    {
        var inProg = _steps.Count(s => s.Status == PlanStatus.InProgress);
        if (inProg > 1)
        {
            for (int i = 0, seen = 0; i < _steps.Count; i++)
            {
                if (_steps[i].Status == PlanStatus.InProgress)
                {
                    if (seen++ > 0) _steps[i] = _steps[i] with { Status = PlanStatus.Pending };
                }
            }
        }
    }
}

