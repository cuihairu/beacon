namespace Beacon.Core.Models;

/// <summary>CI/Workflow 统一生命周期：UI 与底层 CI 系统解耦（RFC §4.1）。</summary>
public enum LifecycleState
{
    Queued,
    Running,
    Success,
    Failed,
    Cancelled,
    Skipped,
    Unknown,
}
