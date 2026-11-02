namespace StudyFlow.Models
{
    /// <summary>
    /// Whether a material or note has reached Yandex Disk.
    /// </summary>
    public enum SyncStatus
    {
        Pending = 0,
        Synced = 1,
        Error = 2
    }
}
