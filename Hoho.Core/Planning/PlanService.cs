using System.Text.Json;

namespace Hoho.Core.Planning;

public sealed class PlanService
{
    private readonly string _path;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private Plan _plan = new();

    public PlanService(string sessionDir)
    {
        Directory.CreateDirectory(sessionDir);
        _path = Path.Combine(sessionDir, "plan.json");
        if (File.Exists(_path))
        {
            try
            {
                var json = File.ReadAllText(_path);
                var steps = JsonSerializer.Deserialize<List<PlanStep>>(json);
                if (steps is not null) _plan.SetSteps(steps);
            }
            catch { /* ignore */ }
        }
    }

    public IReadOnlyList<PlanStep> Steps => _plan.Steps;

    public void SetSteps(IEnumerable<PlanStep> steps)
    {
        _plan.SetSteps(steps);
        Persist();
    }

    public void UpdateStep(int index, PlanStep updated)
    {
        _plan.UpdateStep(index, updated);
        Persist();
    }

    private void Persist()
    {
        var json = JsonSerializer.Serialize(_plan.Steps, _json);
        File.WriteAllText(_path, json);
    }
}

