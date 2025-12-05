using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UniversalAnalogInputUI.Models;

public class GameProfile : INotifyPropertyChanged
{
    private string _name = "";
    private string _description = "";
    private string _gamePath = "";
    private string _hotKey = "";
    private ObservableCollection<SubProfile> _subProfiles = new();
    private uint _sourceIndex;
    private Guid _id = Guid.NewGuid();
    private DateTime _createdAt = DateTime.Now;
    private DateTime _modifiedAt = DateTime.Now;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string GamePath
    {
        get => _gamePath;
        set => SetProperty(ref _gamePath, value);
    }

    public string HotKey
    {
        get => _hotKey;
        set => SetProperty(ref _hotKey, value);
    }

    public ObservableCollection<SubProfile> SubProfiles
    {
        get => _subProfiles;
        set => SetProperty(ref _subProfiles, value);
    }

    public uint SourceIndex
    {
        get => _sourceIndex;
        set => SetProperty(ref _sourceIndex, value);
    }

    public int SubProfilesCount => SubProfiles.Count;

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public DateTime ModifiedAt
    {
        get => _modifiedAt;
        set => SetProperty(ref _modifiedAt, value);
    }

    public GameProfile()
    {
        SubProfiles.Add(new SubProfile
        {
            Id = Guid.NewGuid(),
            Name = "Default",
            Description = "Default control scheme",
            HotKey = "F1"
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public class SubProfile : INotifyPropertyChanged
{
    private string _name = "";
    private string _description = "";
    private string _hotKey = "";
    private ObservableCollection<KeyMapping> _mappings = new();
    private uint _sourceIndex;
    private Guid _id = Guid.NewGuid();
    private DateTime _createdAt = DateTime.Now;
    private DateTime _modifiedAt = DateTime.Now;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string HotKey
    {
        get => _hotKey;
        set => SetProperty(ref _hotKey, value);
    }

    public ObservableCollection<KeyMapping> Mappings
    {
        get => _mappings;
        set => SetProperty(ref _mappings, value);
    }

    public uint SourceIndex
    {
        get => _sourceIndex;
        set => SetProperty(ref _sourceIndex, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public DateTime ModifiedAt
    {
        get => _modifiedAt;
        set => SetProperty(ref _modifiedAt, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
