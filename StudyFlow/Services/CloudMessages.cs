namespace StudyFlow.Services
{
    /// <summary>
    /// Sent through WeakReferenceMessenger after materials are added, renamed or deleted.
    /// </summary>
    public sealed class MaterialsChangedMessage
    {
    }

    /// <summary>
    /// Sent while <see cref="CloudBackupService"/> works, so the settings page can show progress.
    /// </summary>
    public sealed class CloudSyncStatusMessage
    {
    }

    /// <summary>
    /// Sent when a <see cref="CloudBackupService"/> run ends, so lists can refresh their sync icons.
    /// </summary>
    public sealed class CloudSyncFinishedMessage
    {
    }
}
