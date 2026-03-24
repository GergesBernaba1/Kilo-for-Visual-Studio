namespace Kilo.VisualStudio.Contracts.Models
{
    public enum KiloSessionEventKind
    {
        Unknown = 0,
        ConnectionStateChanged = 1,
        SessionCreated = 2,
        SessionUpdated = 3,
        SessionDeleted = 4,
        TurnStarted = 5,
        TurnCompleted = 6,
        MessageUpdated = 7,
        MessageRemoved = 8,
        TextDelta = 9,
        PartUpdated = 10,
        PartRemoved = 11,
        DiffUpdated = 12,
        ToolExecutionUpdated = 13,
        Error = 14
    }
}
