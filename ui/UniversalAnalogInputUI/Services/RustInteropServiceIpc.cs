using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using UniversalAnalogInputUI.Helpers;
using UniversalAnalogInputUI.Interop;
using UniversalAnalogInputUI.Services.Interfaces;

namespace UniversalAnalogInputUI.Services;

/// <summary>
/// IPC-based implementation of IRustInteropService using Named Pipes for communication with the Rust tray app
/// </summary>
public class RustInteropServiceIpc : IRustInteropService, IDisposable
{
    private const string PipeName = "universal-analog-input";
    private const int ConnectTimeoutMs = 5000;
    private const int ReceiveBufferSize = 65536;

    private NamedPipeClientStream? _pipeClient;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private bool _isDisposed;

    public event EventHandler? ShutdownRequested;
    public event EventHandler<KeyboardStatusEventArgs>? KeyboardStatusChanged;
    public event EventHandler? BringToFrontRequested;
    public event EventHandler<UiEventArgs>? UiEventReceived;

    private static int _nextMessageId = 0;

    private Task? _listenerTask;
    private CancellationTokenSource? _listenerCancellation;

    private readonly Dictionary<uint, TaskCompletionSource<IpcResponse>> _pendingResponses = new();
    private readonly object _pendingResponsesLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
            new ByteArrayJsonConverter()
        }
    };

    public RustInteropServiceIpc()
    {
    }

    private static uint GetNextMessageId()
    {
        return (uint)Interlocked.Increment(ref _nextMessageId);
    }

    private async Task EnsureConnectedAsync()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(RustInteropServiceIpc));

        if (_pipeClient?.IsConnected == true)
            return;

        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_pipeClient?.IsConnected == true)
                return;

            _pipeClient?.Dispose();

            _pipeClient = new NamedPipeClientStream(
                serverName: ".",
                pipeName: PipeName,
                direction: PipeDirection.InOut,
                options: PipeOptions.Asynchronous);

            try
            {
                using var cts = new CancellationTokenSource(ConnectTimeoutMs);
                await _pipeClient.ConnectAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new IOException("Failed to connect to IPC server within timeout");
            }

            _pipeClient.ReadMode = PipeTransmissionMode.Byte;
            StartNotificationListener();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Reads messages from the pipe and dispatches based on message_id correlation.
    /// Messages with message_id complete pending requests; messages without are notifications.
    /// </summary>
    private void StartNotificationListener()
    {
        _listenerCancellation?.Cancel();
        _listenerCancellation = new CancellationTokenSource();

        _listenerTask = Task.Run(() =>
        {
            while (!_listenerCancellation.Token.IsCancellationRequested && _pipeClient?.IsConnected == true)
            {
                try
                {
                    var lengthBytes = new byte[4];
                    ReadExactly(_pipeClient, lengthBytes);
                    var messageLength = BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);

                    if (messageLength > 1024 * 1024)
                        break;

                    var payload = new byte[messageLength];
                    ReadExactly(_pipeClient, payload);

                    var messageJson = Encoding.UTF8.GetString(payload);
                    var response = JsonSerializer.Deserialize<IpcResponse>(messageJson, JsonOptions);

                    if (response == null)
                        continue;

                    if (response.MessageId.HasValue)
                    {
                        var messageId = response.MessageId.Value;
                        TaskCompletionSource<IpcResponse>? tcs = null;
                        lock (_pendingResponsesLock)
                        {
                            if (_pendingResponses.TryGetValue(messageId, out tcs))
                                _pendingResponses.Remove(messageId);
                        }

                        if (tcs != null)
                            tcs.SetResult(response);
                    }
                    else
                    {
                        if (response.Type == "Shutdown")
                        {
                            ShutdownRequested?.Invoke(this, EventArgs.Empty);
                            break;
                        }
                        else if (response.Type == "UiEvent" && response.Data != null)
                        {
                            var eventData = JsonSerializer.Deserialize<UiEventData>(
                                ((JsonElement)response.Data).GetRawText(), JsonOptions);

                            if (eventData != null)
                                UiEventReceived?.Invoke(this, new UiEventArgs(eventData));
                        }
                        else if (response.Type == "KeyboardStatus" && response.Connected.HasValue)
                        {
                            KeyboardStatusChanged?.Invoke(this, new KeyboardStatusEventArgs(response.Connected.Value));
                        }
                        else if (response.Type == "BringToFront")
                        {
                            BringToFrontRequested?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }
                catch (Exception ex) when (ex is OperationCanceledException or IOException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Silently continue on non-fatal errors; connection will be re-established on next request
                }
            }

            List<TaskCompletionSource<IpcResponse>> pendingTcs;
            lock (_pendingResponsesLock)
            {
                pendingTcs = _pendingResponses.Values.ToList();
                _pendingResponses.Clear();
            }

            foreach (var tcs in pendingTcs)
                tcs.TrySetException(new IOException("IPC listener stopped - connection lost"));

            _pipeClient?.Dispose();
            _pipeClient = null;
        }, _listenerCancellation.Token);
    }

    /// <summary>
    /// Sends a command to the Rust IPC server and awaits the correlated response.
    /// </summary>
    private async Task<IpcResponse> SendCommandAsync(IpcCommand command)
    {
        await EnsureConnectedAsync().ConfigureAwait(false);

        var messageId = GetNextMessageId();
        command.MessageId = messageId;

        var tcs = new TaskCompletionSource<IpcResponse>();

        lock (_pendingResponsesLock)
        {
            _pendingResponses[messageId] = tcs;
        }

        await _requestLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_pipeClient?.IsConnected != true)
                throw new InvalidOperationException("Not connected to IPC server");

            var json = JsonSerializer.Serialize(command, JsonOptions);
            var payloadBytes = Encoding.UTF8.GetBytes(json);

            var lengthBytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(lengthBytes, (uint)payloadBytes.Length);
            await _pipeClient.WriteAsync(lengthBytes, 0, lengthBytes.Length).ConfigureAwait(false);
            await _pipeClient.WriteAsync(payloadBytes, 0, payloadBytes.Length).ConfigureAwait(false);
            await _pipeClient.FlushAsync().ConfigureAwait(false);
        }
        catch (IOException)
        {
            lock (_pendingResponsesLock)
            {
                _pendingResponses.Remove(messageId);
            }

            _pipeClient?.Dispose();
            _pipeClient = null;
            throw;
        }
        finally
        {
            _requestLock.Release();
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var responseTask = tcs.Task;
        var timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);

        var completedTask = await Task.WhenAny(responseTask, timeoutTask).ConfigureAwait(false);

        if (completedTask == timeoutTask)
        {
            lock (_pendingResponsesLock)
            {
                _pendingResponses.Remove(messageId);
            }
            throw new TimeoutException($"IPC request timeout for message_id={messageId}");
        }

        return await responseTask.ConfigureAwait(false);
    }

    private static void ReadExactly(Stream stream, byte[] buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer, totalRead, buffer.Length - totalRead);
            if (read == 0)
                throw new IOException("Pipe closed unexpectedly");

            totalRead += read;
        }
    }

    private int SendIntCommand(IpcCommand command)
    {
        var response = SendCommandAsync(command).GetAwaiter().GetResult();
        return response.Type switch
        {
            "Success" => 0,
            "IntValue" => response.IntValue ?? -1,
            "Error" => -1,
            _ => -1
        };
    }

    private uint SendUintCommand(IpcCommand command)
    {
        var response = SendCommandAsync(command).GetAwaiter().GetResult();
        return response.Type switch
        {
            "UintValue" => response.UintValue ?? 0,
            _ => 0
        };
    }

    private string SendStringCommand(IpcCommand command)
    {
        var response = SendCommandAsync(command).GetAwaiter().GetResult();
        return response.Type == "StringValue" ? response.StringValue ?? "" : "";
    }

    public void Cleanup()
    {
        Dispose();
    }

    public string GetVersion()
    {
        return SendStringCommand(new IpcCommand { Type = "GetVersion" });
    }

    public Models.PerformanceMetrics? GetPerformanceMetrics()
    {
        try
        {
            var response = SendCommandAsync(new IpcCommand { Type = "GetPerformanceMetrics" }).GetAwaiter().GetResult();
            if (response.Type == "PerformanceMetrics" && response.Data != null)
            {
                return JsonSerializer.Deserialize<Models.PerformanceMetrics>(response.Data.ToString() ?? "{}", JsonOptions);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public int StartMapping() => SendIntCommand(new IpcCommand { Type = "StartMapping" });
    public int StopMapping() => SendIntCommand(new IpcCommand { Type = "StopMapping" });
    public bool IsMappingActive() => SendIntCommand(new IpcCommand { Type = "IsMappingActive" }) == 1;

    public int CreateProfile(string profileName, string description) => SendIntCommand(new IpcCommand
    {
        Type = "CreateProfile",
        Name = profileName,
        Description = description
    });

    public int DeleteProfile(Guid profileId) => SendIntCommand(new IpcCommand
    {
        Type = "DeleteProfile",
        ProfileId = GuidToIpcBytes(profileId)
    });

    public int RenameProfile(Guid profileId, string newName) => SendIntCommand(new IpcCommand
    {
        Type = "RenameProfile",
        ProfileId = GuidToIpcBytes(profileId),
        NewName = newName
    });

    public int UpdateProfileDescription(Guid profileId, string description) => SendIntCommand(new IpcCommand
    {
        Type = "UpdateProfileDescription",
        ProfileId = GuidToIpcBytes(profileId),
        Description = description
    });

    public int UpdateProfileHotkey(Guid profileId, string hotkey) => SendIntCommand(new IpcCommand
    {
        Type = "UpdateProfileHotkey",
        ProfileId = GuidToIpcBytes(profileId),
        Hotkey = hotkey
    });

    public int SwitchProfile(Guid profileId, Guid subProfileId) => SendIntCommand(new IpcCommand
    {
        Type = "SwitchProfile",
        ProfileId = GuidToIpcBytes(profileId),
        SubProfileId = GuidToIpcBytes(subProfileId)
    });

    public int AddSubProfile(Guid profileId, string subProfileName, string description, string hotkey) => SendIntCommand(new IpcCommand
    {
        Type = "AddSubProfile",
        ProfileId = GuidToIpcBytes(profileId),
        Name = subProfileName,
        Description = description,
        Hotkey = hotkey
    });

    public int RenameSubProfile(Guid profileId, Guid subProfileId, string newSubProfileName) => SendIntCommand(new IpcCommand
    {
        Type = "RenameSubProfile",
        ProfileId = GuidToIpcBytes(profileId),
        SubId = GuidToIpcBytes(subProfileId),
        NewName = newSubProfileName
    });

    public int DeleteSubProfile(Guid profileId, Guid subProfileId) => SendIntCommand(new IpcCommand
    {
        Type = "DeleteSubProfile",
        ProfileId = GuidToIpcBytes(profileId),
        SubId = GuidToIpcBytes(subProfileId)
    });

    public int UpdateSubProfileHotkey(Guid profileId, Guid subProfileId, string hotkey) => SendIntCommand(new IpcCommand
    {
        Type = "UpdateSubProfileHotkey",
        ProfileId = GuidToIpcBytes(profileId),
        SubId = GuidToIpcBytes(subProfileId),
        Hotkey = hotkey
    });

    public int SuspendHotkeys() => SendIntCommand(new IpcCommand { Type = "SuspendHotkeys" });
    public int ResumeHotkeys() => SendIntCommand(new IpcCommand { Type = "ResumeHotkeys" });

    public int SetMapping(Guid profileId, Guid subProfileId, ref CMappingInfo mapping)
    {
        var mappingInfo = new MappingInfo
        {
            KeyName = RustDataConverter.DecodeUtf8(mapping.KeyName),
            GamepadControl = RustDataConverter.DecodeUtf8(mapping.GamepadControl),
            ResponseCurve = RustDataConverter.DecodeUtf8(mapping.ResponseCurve),
            DeadZoneInner = mapping.DeadZoneInner,
            DeadZoneOuter = mapping.DeadZoneOuter,
            UseSmoothCurve = mapping.UseSmoothCurve != 0,
            CustomPointCount = mapping.CustomPointCount,
            CustomPoints = ConvertCustomPoints(mapping.CustomPoints, mapping.CustomPointCount),
            CreatedAt = (long)mapping.CreatedAt
        };

        return SendIntCommand(new IpcCommand
        {
            Type = "SetMapping",
            ProfileId = GuidToIpcBytes(profileId),
            SubProfileId = GuidToIpcBytes(subProfileId),
            Mapping = mappingInfo
        });
    }

    public int RemoveMapping(Guid profileId, Guid subProfileId, string keyName) => SendIntCommand(new IpcCommand
    {
        Type = "RemoveMapping",
        ProfileId = GuidToIpcBytes(profileId),
        SubProfileId = GuidToIpcBytes(subProfileId),
        KeyName = keyName
    });

    public uint GetCurrentMappingCount() => SendUintCommand(new IpcCommand { Type = "GetCurrentMappingCount" });

    public int GetCurrentMappingInfo(uint index, out CMappingInfo info)
    {
        var response = SendCommandAsync(new IpcCommand
        {
            Type = "GetCurrentMappingInfo",
            Index = index
        }).GetAwaiter().GetResult();

        if (response.Type == "MappingInfo" && response.Data != null)
        {
            var mappingInfo = JsonSerializer.Deserialize<MappingInfo>(
                ((JsonElement)response.Data).GetRawText(), JsonOptions);

            if (mappingInfo != null)
            {
                info = ConvertToC(mappingInfo);
                return 0;
            }
        }

        info = default;
        return -1;
    }

    public uint GetProfileMetadataCount() => SendUintCommand(new IpcCommand { Type = "GetProfileMetadataCount" });

    public int GetProfileMetadata(uint index, out CProfileMetadata metadata)
    {
        var response = SendCommandAsync(new IpcCommand
        {
            Type = "GetProfileMetadata",
            Index = index
        }).GetAwaiter().GetResult();

        if (response.Type == "ProfileMetadata" && response.Data != null)
        {
            var profileMeta = JsonSerializer.Deserialize<ProfileMetadata>(
                ((JsonElement)response.Data).GetRawText(), JsonOptions);

            if (profileMeta != null)
            {
                metadata = ConvertToC(profileMeta);
                return 0;
            }
        }

        metadata = default;
        return -1;
    }

    public int GetSubProfileMetadata(uint profileIndex, uint subIndex, out CSubProfileMetadata metadata)
    {
        var response = SendCommandAsync(new IpcCommand
        {
            Type = "GetSubProfileMetadata",
            ProfileIdx = profileIndex,
            SubIdx = subIndex
        }).GetAwaiter().GetResult();

        if (response.Type == "SubProfileMetadata" && response.Data != null)
        {
            var subProfileMeta = JsonSerializer.Deserialize<SubProfileMetadata>(
                ((JsonElement)response.Data).GetRawText(), JsonOptions);

            if (subProfileMeta != null)
            {
                metadata = ConvertToC(subProfileMeta);
                return 0;
            }
        }

        metadata = default;
        return -1;
    }

    public uint GetSupportedKeyCount() => SendUintCommand(new IpcCommand { Type = "GetSupportedKeyCount" });

    public string GetSupportedKeyName(uint index) => SendStringCommand(new IpcCommand
    {
        Type = "GetSupportedKeyName",
        Index = index
    });

    public uint GetGamepadControlCount() => SendUintCommand(new IpcCommand { Type = "GetGamepadControlCount" });

    public string GetGamepadControlName(uint index) => SendStringCommand(new IpcCommand
    {
        Type = "GetGamepadControlName",
        Index = index
    });

    public int SaveProfileToFile(Guid profileId, string filePath) => SendIntCommand(new IpcCommand
    {
        Type = "SaveProfileToFile",
        ProfileId = GuidToIpcBytes(profileId),
        FilePath = filePath
    });

    public int LoadProfileFromFile(string filePath) => SendIntCommand(new IpcCommand
    {
        Type = "LoadProfileFromFile",
        FilePath = filePath
    });

    public uint GetUiMessageId() => 0;
    public uint GetUiEventTypeSubProfileSwitch() => 0;
    public void RegisterUiWindow(IntPtr hwnd) { }

    public bool NextUiEvent(out CUiEvent evt)
    {
        evt = default;
        return false;
    }

    private static List<List<float>> ConvertCustomPoints(CCurvePoint[] points, byte count)
    {
        var result = new List<List<float>>();
        for (int i = 0; i < Math.Min(count, points.Length); i++)
        {
            result.Add(new List<float> { points[i].X, points[i].Y });
        }
        return result;
    }

    private static CMappingInfo ConvertToC(MappingInfo info)
    {
        var result = new CMappingInfo
        {
            KeyName = StringToBytes(info.KeyName, 32),
            GamepadControl = StringToBytes(info.GamepadControl, 32),
            ResponseCurve = StringToBytes(info.ResponseCurve, 32),
            DeadZoneInner = info.DeadZoneInner,
            DeadZoneOuter = info.DeadZoneOuter,
            UseSmoothCurve = (byte)(info.UseSmoothCurve ? 1 : 0),
            CustomPointCount = (byte)info.CustomPointCount,
            CustomPoints = new CCurvePoint[16],
            CreatedAt = (ulong)info.CreatedAt
        };

        for (int i = 0; i < Math.Min(info.CustomPoints.Count, 16); i++)
        {
            result.CustomPoints[i] = new CCurvePoint
            {
                X = info.CustomPoints[i][0],
                Y = info.CustomPoints[i][1]
            };
        }

        return result;
    }

    private static CProfileMetadata ConvertToC(ProfileMetadata meta)
    {
        return new CProfileMetadata
        {
            Id = NormalizeGuidBytes(meta.Id),
            Name = StringToBytes(meta.Name, 64),
            Description = StringToBytes(meta.Description, 256),
            SubProfileCount = meta.SubProfileCount,
            CreatedAt = (ulong)meta.CreatedAt,
            ModifiedAt = (ulong)meta.ModifiedAt,
            Hotkey = StringToBytes(meta.Hotkey, 32)
        };
    }

    private static CSubProfileMetadata ConvertToC(SubProfileMetadata meta)
    {
        return new CSubProfileMetadata
        {
            Id = NormalizeGuidBytes(meta.Id),
            ParentProfileId = NormalizeGuidBytes(meta.ParentProfileId),
            Name = StringToBytes(meta.Name, 64),
            Description = StringToBytes(meta.Description, 256),
            Hotkey = StringToBytes(meta.Hotkey, 32),
            CreatedAt = (ulong)meta.CreatedAt,
            ModifiedAt = (ulong)meta.ModifiedAt
        };
    }

    private static byte[] NormalizeGuidBytes(byte[]? input)
    {
        var normalized = new byte[16];
        if (input == null)
            return normalized;

        Array.Copy(input, normalized, Math.Min(16, input.Length));
        return normalized;
    }

    private static byte[] GuidToIpcBytes(Guid guid)
    {
        // Match Rust's `Uuid::to_bytes_le()` which is identical to Guid.ToByteArray()
        return guid.ToByteArray();
    }

    private static byte[] StringToBytes(string str, int size)
    {
        var bytes = new byte[size];
        if (!string.IsNullOrEmpty(str))
        {
            var utf8 = Encoding.UTF8.GetBytes(str);
            Array.Copy(utf8, bytes, Math.Min(utf8.Length, size - 1));
        }
        return bytes;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        _listenerCancellation?.Cancel();

        try
        {
            _pipeClient?.Dispose();
        }
        catch { }

        if (_listenerTask != null && !_listenerTask.IsCompleted)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _listenerTask.ConfigureAwait(false);
                }
                catch { }
            });
        }

        _connectionLock?.Dispose();
        _requestLock?.Dispose();
        _listenerCancellation?.Dispose();

        GC.SuppressFinalize(this);
    }
}

public class IpcCommand
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    // Message correlation ID for request/response matching
    [JsonPropertyName("message_id")]
    public uint? MessageId { get; set; }

    // Generic parameters
    [JsonPropertyName("index")]
    public uint? Index { get; set; }

    [JsonPropertyName("profile_id")]
    public byte[]? ProfileId { get; set; }

    [JsonPropertyName("sub_profile_id")]
    public byte[]? SubProfileId { get; set; }

    [JsonPropertyName("sub_id")]
    public byte[]? SubId { get; set; }

    [JsonPropertyName("profile_idx")]
    public uint? ProfileIdx { get; set; }

    [JsonPropertyName("sub_idx")]
    public uint? SubIdx { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("new_name")]
    public string? NewName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("hotkey")]
    public string? Hotkey { get; set; }

    [JsonPropertyName("key_name")]
    public string? KeyName { get; set; }

    [JsonPropertyName("file_path")]
    public string? FilePath { get; set; }

    [JsonPropertyName("mapping")]
    public MappingInfo? Mapping { get; set; }
}

public class IpcResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("message_id")]
    public uint? MessageId { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("connected")]
    public bool? Connected { get; set; }

    [JsonIgnore]
    public int? IntValue => Value?.ValueKind == JsonValueKind.Number ? Value?.GetInt32() : null;

    [JsonIgnore]
    public uint? UintValue => Value?.ValueKind == JsonValueKind.Number ? Value?.GetUInt32() : null;

    [JsonIgnore]
    public string? StringValue => Value?.ValueKind == JsonValueKind.String ? Value?.GetString() : null;
}

public class ProfileMetadata
{
    [JsonPropertyName("id")]
    public byte[] Id { get; set; } = new byte[16];

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("sub_profile_count")]
    public uint SubProfileCount { get; set; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("modified_at")]
    public long ModifiedAt { get; set; }

    [JsonPropertyName("hotkey")]
    public string Hotkey { get; set; } = "";
}

public class SubProfileMetadata
{
    [JsonPropertyName("id")]
    public byte[] Id { get; set; } = new byte[16];

    [JsonPropertyName("parent_profile_id")]
    public byte[] ParentProfileId { get; set; } = new byte[16];

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("hotkey")]
    public string Hotkey { get; set; } = "";

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("modified_at")]
    public long ModifiedAt { get; set; }
}

public class MappingInfo
{
    [JsonPropertyName("key_name")]
    public string KeyName { get; set; } = "";

    [JsonPropertyName("gamepad_control")]
    public string GamepadControl { get; set; } = "";

    [JsonPropertyName("response_curve")]
    public string ResponseCurve { get; set; } = "";

    [JsonPropertyName("dead_zone_inner")]
    public float DeadZoneInner { get; set; }

    [JsonPropertyName("dead_zone_outer")]
    public float DeadZoneOuter { get; set; }

    [JsonPropertyName("use_smooth_curve")]
    public bool UseSmoothCurve { get; set; }

    [JsonPropertyName("custom_point_count")]
    public uint CustomPointCount { get; set; }

    [JsonPropertyName("custom_points")]
    public List<List<float>> CustomPoints { get; set; } = new();

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }
}

public class ByteArrayJsonConverter : JsonConverter<byte[]>
{
    public override byte[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var base64 = reader.GetString();
            return string.IsNullOrEmpty(base64) ? null : Convert.FromBase64String(base64);
        }

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected byte array");

        var bytes = new List<byte>(16);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.Number || !reader.TryGetByte(out var value))
            {
                throw new JsonException("Invalid byte value");
            }

            bytes.Add(value);
        }

        return bytes.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, byte[]? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var b in value)
        {
            writer.WriteNumberValue(b);
        }
        writer.WriteEndArray();
    }
}

public class UiEventData
{
    [JsonPropertyName("event_type")]
    public uint EventType { get; set; }

    [JsonPropertyName("profile_id")]
    public byte[] ProfileId { get; set; } = new byte[16];

    [JsonPropertyName("sub_profile_id")]
    public byte[] SubProfileId { get; set; } = new byte[16];
}

public class UiEventArgs : EventArgs
{
    public UiEventData EventData { get; }

    public UiEventArgs(UiEventData eventData)
    {
        EventData = eventData;
    }
}

public class KeyboardStatusEventArgs : EventArgs
{
    public bool IsConnected { get; }

    public KeyboardStatusEventArgs(bool isConnected)
    {
        IsConnected = isConnected;
    }
}
