namespace StudyFlow.Services
{
    /// <summary>
    /// Sent through WeakReferenceMessenger after a note is saved or deleted, so open lists can reload.
    /// The note page saves on leaving, which can finish after the previous page has already appeared.
    /// </summary>
    public sealed class NotesChangedMessage
    {
    }
}
