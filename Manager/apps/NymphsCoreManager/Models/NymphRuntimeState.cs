namespace NymphsCoreManager.Models;

public enum NymphRuntimeState
{
    Unknown = 0,
    NotApplicable = 1,
    Stopped = 2,
    Starting = 3,
    Running = 4,
    Degraded = 5,
    Failed = 6,
    Installed = 7,
    Available = 8
}
