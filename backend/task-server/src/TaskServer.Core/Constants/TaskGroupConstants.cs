namespace TaskServer.Core.Constants;

public static class TaskGroupConstants
{
    public static readonly Guid DefaultGroupId = Guid.Empty;

    public const string DefaultGroupName = "default";
    public const string CpuProcessingGroupName = "cpu-processing";
    public const string ExclusiveProcessingGroupName = "exclusive-processing";

    public const int DefaultGroupMaxParallelism = 32;
    public const int ExclusiveGroupMaxParallelism = 1;
}
