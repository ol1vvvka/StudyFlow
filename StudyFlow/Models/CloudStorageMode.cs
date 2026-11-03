namespace StudyFlow.Models
{
    /// <summary>
    /// Where material files live after a successful upload to Yandex Disk.
    /// Metadata (titles, notes text, calendar) always stays in the local SQLite database.
    /// </summary>
    public enum CloudStorageMode
    {
        /// <summary>Keep files on the device and mirror them to Disk.</summary>
        DeviceAndCloud = 0,

        /// <summary>After upload, remove the local file; download again when opening.</summary>
        CloudOnly = 1
    }
}
