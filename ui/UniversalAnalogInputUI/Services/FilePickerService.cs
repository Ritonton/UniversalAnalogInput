using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using UniversalAnalogInputUI.Services.Interfaces;
using UniversalAnalogInputUI.Services.Factories;

namespace UniversalAnalogInputUI.Services;

/// <summary>
/// File picker service implementation using Windows Storage APIs
/// </summary>
public class FilePickerService : IFilePickerService
{
    private readonly IWindowHandleProvider _windowHandleProvider;

    public FilePickerService(IWindowHandleProvider windowHandleProvider)
    {
        _windowHandleProvider = windowHandleProvider;
    }

    public async Task<string?> PickImportFileAsync()
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };

        picker.FileTypeFilter.Add(".json");
        picker.FileTypeFilter.Add(".uai");

        var hwnd = _windowHandleProvider.Handle;
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        StorageFile? file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickExportFileAsync(string suggestedFileName)
    {
        var savePicker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = string.IsNullOrWhiteSpace(suggestedFileName) ? "profile" : suggestedFileName
        };

        savePicker.FileTypeChoices.Add("Universal Analog Input Profile", new List<string> { ".json" });

        var hwnd = _windowHandleProvider.Handle;
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

        StorageFile? file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            CachedFileManager.DeferUpdates(file);
        }
        return file?.Path;
    }

    public async Task<bool> CompleteExportUpdatesAsync(string filePath)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            await CachedFileManager.CompleteUpdatesAsync(file);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
