using System;
using System.IO.MemoryMappedFiles;
using System.Text;
using Microsoft.UI.Xaml;
using UniversalAnalogInputUI.Services.Interfaces;

namespace UniversalAnalogInputUI.Services;

// Reads the Wooting analog buffer from the Memory Mapped File "Local\UAI_AnalogStream" written by the tray.
//
// MMF layout (784 bytes):
//   [0..8]  sequence:      u64  — odd = write in progress
//   [8]     key_count:     u8
//   [9]     stream_active: u8   — 1 = running, 0 = stopped
//   [10..16] padding
//   [16..]  entries[32] × 24 bytes:
//             [+0] analog_value : f32
//             [+4] key_code     : u16
//             [+6] name_len     : u8
//             [+8] name         : [u8;16] ASCII
public sealed class AnalogStreamService : IAnalogStreamService, IDisposable
{
    private const string MmfName = "Local\\UAI_AnalogStream";
    private const int MmfSize = 784;
    private const int EntrySize = 24;
    private const int EntriesOffset = 16;
    private const int MaxEntries = 32;
    private const int TimerIntervalMs = 16; // ~60 Hz

    public event EventHandler<AnalogSnapshot>? DataUpdated;

    private DispatcherTimer? _timer;
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _view;
    private ulong _lastSequence;

    public void Start()
    {
        Stop();

        try
        {
            _mmf = MemoryMappedFile.OpenExisting(MmfName, MemoryMappedFileRights.Read);
            _view = _mmf.CreateViewAccessor(0, MmfSize, MemoryMappedFileAccess.Read);
        }
        catch
        {
            // Memory Mapped File not ready yet, will retry on first tick.
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TimerIntervalMs) };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
        DisposeView();
        _lastSequence = 0;
    }

    private void OnTick(object? sender, object e)
    {
        if (_view == null)
        {
            try
            {
                _mmf = MemoryMappedFile.OpenExisting(MmfName, MemoryMappedFileRights.Read);
                _view = _mmf.CreateViewAccessor(0, MmfSize, MemoryMappedFileAccess.Read);
            }
            catch
            {
                return;
            }
        }

        try
        {
            var snapshot = ReadSnapshot();
            if (snapshot != null)
                DataUpdated?.Invoke(this, snapshot);
        }
        catch
        {
            // Tray crashed, reset and wait for reconnect.
            DisposeView();
            DataUpdated?.Invoke(this, AnalogSnapshot.StreamStopped);
        }
    }

    private AnalogSnapshot? ReadSnapshot()
    {
        var view = _view!;

        ulong seq1 = view.ReadUInt64(0);

        if ((seq1 & 1) != 0)    // write in progress
            return null;

        if (seq1 == _lastSequence)
            return null;

        byte streamActive = view.ReadByte(9);
        int keyCount = Math.Min(view.ReadByte(8), (byte)MaxEntries);

        AnalogKeyEntry[] keys = Array.Empty<AnalogKeyEntry>();

        if (streamActive == 1 && keyCount > 0)
        {
            keys = new AnalogKeyEntry[keyCount];
            for (int i = 0; i < keyCount; i++)
            {
                int offset = EntriesOffset + i * EntrySize;
                float value    = view.ReadSingle(offset);
                ushort keyCode = view.ReadUInt16(offset + 4);
                byte nameLen   = Math.Min(view.ReadByte(offset + 6), (byte)15);

                var nameBytes = new byte[nameLen];
                if (nameLen > 0)
                    view.ReadArray(offset + 8, nameBytes, 0, nameLen);

                keys[i] = new AnalogKeyEntry
                {
                    KeyCode = keyCode,
                    KeyName = nameLen > 0 ? Encoding.ASCII.GetString(nameBytes) : $"0x{keyCode:X}",
                    Value   = Math.Clamp(value, 0f, 1f),
                };
            }
            Array.Sort(keys, (a, b) =>
            {
                int cmp = b.Value.CompareTo(a.Value);
                return cmp != 0 ? cmp : a.KeyCode.CompareTo(b.KeyCode);
            });
        }

        // Seqlock: verify no write happened mid-read.
        ulong seq2 = view.ReadUInt64(0);
        if (seq2 != seq1)
            return null;

        _lastSequence = seq1;

        if (streamActive == 0)
            return AnalogSnapshot.StreamStopped;

        return new AnalogSnapshot { Keys = keys };
    }

    private void DisposeView()
    {
        _view?.Dispose();
        _view = null;
        _mmf?.Dispose();
        _mmf = null;
    }

    public void Dispose() => Stop();
}
