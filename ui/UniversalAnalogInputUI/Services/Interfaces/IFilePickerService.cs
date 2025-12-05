using System.Threading.Tasks;

namespace UniversalAnalogInputUI.Services.Interfaces;

/// <summary>Abstracts file picker operations for profile import/export.</summary>
public interface IFilePickerService
{
    /// <summary>Opens a file picker for importing profiles.</summary>
    /// <returns>Selected file path or null if cancelled</returns>
    Task<string?> PickImportFileAsync();

    /// <summary>Opens a save picker for exporting profiles.</summary>
    /// <param name="suggestedFileName">Suggested filename for the export</param>
    /// <returns>Selected file path or null if cancelled</returns>
    Task<string?> PickExportFileAsync(string suggestedFileName);

    /// <summary>Completes cached file updates after an export.</summary>
    /// <param name="filePath">Path to the exported file</param>
    /// <returns>True if updates completed successfully</returns>
    Task<bool> CompleteExportUpdatesAsync(string filePath);
}
