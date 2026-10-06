using Beacon.Core.Abstractions;
using Beacon.Core.Models;

namespace Beacon.Actions;

/// <summary>模板变量合并（Actions 内共用）：Vars + SourceState.Payload（后者覆盖前者）。</summary>
internal static class ActionVars
{
    public static Dictionary<string, string> Merge(ActionExecutionContext context)
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in context.Vars)
        {
            vars[key] = value;
        }
        if (context.SourceState is { } state)
        {
            foreach (var (key, value) in state.Payload)
            {
                vars[key] = value;
            }
        }
        return vars;
    }
}
