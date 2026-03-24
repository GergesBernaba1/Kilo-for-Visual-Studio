namespace Kilo.VisualStudio.Contracts.Models
{
    public enum KiloSessionStatus
    {
        Unknown = 0,
        Idle = 1,
        Running = 2,
        Retry = 3,
        Completed = 4,
        Failed = 5,
        Aborted = 6
    }
}
