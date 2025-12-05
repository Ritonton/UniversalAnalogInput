using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Media;
using UniversalAnalogInputUI.Models;
using UniversalAnalogInputUI.Services.Interfaces;

namespace UniversalAnalogInputUI.Helpers;

/// <summary>Manages profile navigation UI for MainWindow.</summary>
public class ProfileNavigationHelper
{
    private readonly NavigationView _navigationView;
    private readonly IProfileManagementService _profileService;
    private bool _isRestoringSelection;

    public event EventHandler<GameProfile>? RenameProfileRequested;
    public event EventHandler<GameProfile>? ExportProfileRequested;
    public event EventHandler<GameProfile>? DeleteProfileRequested;
    public event EventHandler<(GameProfile Profile, SubProfile SubProfile)>? RenameSubProfileRequested;
    public event EventHandler<(GameProfile Profile, SubProfile SubProfile)>? DeleteSubProfileRequested;

    public event Action<Dictionary<Guid, bool>>? RefreshNavigationRequested;
    public event Action? ClearMappingsRequested;
    public event Action? UpdateSelectionHeaderRequested;
    public event Action? UpdateMappingCountRequested;

    public ProfileNavigationHelper(NavigationView navigationView, IProfileManagementService profileService)
    {
        _navigationView = navigationView;
        _profileService = profileService;

        _profileService.Profiles.CollectionChanged += Profiles_CollectionChanged;
    }

    /// <summary>Unsubscribes from collection change events to prevent memory leaks.</summary>
    public void Dispose()
    {
        _profileService.Profiles.CollectionChanged -= Profiles_CollectionChanged;

        foreach (var profile in _profileService.Profiles)
        {
            profile.SubProfiles.CollectionChanged -= SubProfiles_CollectionChanged;
        }
    }

    /// <summary>Creates NavigationViewItem for profile with sub-profiles as children.</summary>
    public NavigationViewItem CreateProfileNavigationItem(GameProfile profile)
    {
        bool expandForSelectedSub = _profileService.SelectedSubProfile != null && profile.SubProfiles.Contains(_profileService.SelectedSubProfile);

        string profileLabel = BuildProfileLabel(profile);

        var profileItem = new NavigationViewItem
        {
            Content = CreateProfileContent(profileLabel),
            Tag = profile,
            IsExpanded = expandForSelectedSub,
            SelectsOnInvoked = false
        };

        string profileToolTip = string.IsNullOrWhiteSpace(profile.Description)
            ? profileLabel
            : $"{profileLabel}\n{profile.Description}";
        ToolTipService.SetToolTip(profileItem, profileToolTip);

        foreach (var subProfile in profile.SubProfiles)
        {
            profileItem.MenuItems.Add(CreateSubProfileNavigationItem(profile, subProfile));
        }

        profileItem.ContextFlyout = BuildProfileContextFlyout(profile);

        return profileItem;
    }

    /// <summary>Creates NavigationViewItem for sub-profile.</summary>
    public NavigationViewItem CreateSubProfileNavigationItem(GameProfile profile, SubProfile subProfile)
    {
        string label = string.IsNullOrWhiteSpace(subProfile.HotKey)
            ? subProfile.Name
            : $"{subProfile.Name} ({subProfile.HotKey})";

        var subItem = new NavigationViewItem
        {
            Content = new TextBlock { Text = label, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis },
            Tag = subProfile
        };

        string subToolTip = string.IsNullOrWhiteSpace(subProfile.Description)
            ? label
            : $"{label}\n{subProfile.Description}";
        ToolTipService.SetToolTip(subItem, subToolTip);

        subItem.ContextFlyout = BuildSubProfileContextFlyout(profile, subProfile);

        return subItem;
    }

    /// <summary>Updates profile items between full labels and compact icons based on pane state.</summary>
    public void UpdateProfileIconsForPane(bool isPaneOpen)
    {
        const double compactIconBoxSize = 56;

        foreach (var item in _navigationView.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.Tag is GameProfile profile)
            {
                if (isPaneOpen)
                {
                    item.Icon = null;
                    item.Content = CreateProfileContent(BuildProfileLabel(profile));
                    item.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    item.Padding = new Thickness(12, item.Padding.Top, 12, item.Padding.Bottom);
                    item.Resources.Remove("NavigationViewItemOnLeftIconBoxColumnWidth");
                }
                else
                {
                    item.Resources["NavigationViewItemOnLeftIconBoxColumnWidth"] = compactIconBoxSize;
                    item.Icon = CreateCollapsedProfileIcon(GetInitials(profile.Name));
                    item.Content = null;
                    item.HorizontalContentAlignment = HorizontalAlignment.Center;
                    item.Padding = new Thickness(0);
                }

                foreach (var subItem in item.MenuItems.OfType<NavigationViewItem>())
                {
                    if (subItem.Tag is SubProfile subProfile)
                    {
                        subItem.Icon = null;
                        subItem.Content = CreateProfileContent(BuildProfileLabel(profile, subProfile));
                        subItem.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    }
                }
            }
        }
    }

    private static string BuildProfileLabel(GameProfile profile)
    {
        return string.IsNullOrWhiteSpace(profile.HotKey)
            ? profile.Name
            : $"{profile.Name} ({profile.HotKey})";
    }

    private static string BuildProfileLabel(GameProfile profile, SubProfile subProfile)
    {
        var baseName = string.IsNullOrWhiteSpace(subProfile.HotKey)
            ? subProfile.Name
            : $"{subProfile.Name} ({subProfile.HotKey})";
        return baseName;
    }

    private static TextBlock CreateProfileContent(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }

    private static IconElement CreateCollapsedProfileIcon(string initials)
    {
        return new FontIcon
        {
            Glyph = initials,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 30,
            Width = 64,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
        }

        return $"{parts[0][0]}{parts[1][0]}".ToUpper();
    }

    private MenuFlyout BuildProfileContextFlyout(GameProfile profile)
    {
        var flyout = new MenuFlyout();

        var renameItem = new MenuFlyoutItem
        {
            Text = "Rename Profile",
            Tag = profile
        };
        renameItem.Click += (s, e) => RenameProfileRequested?.Invoke(this, profile);
        flyout.Items.Add(renameItem);

        var exportItem = new MenuFlyoutItem
        {
            Text = "Export Profile",
            Tag = profile
        };
        exportItem.Click += (s, e) => ExportProfileRequested?.Invoke(this, profile);
        flyout.Items.Add(exportItem);

        var deleteItem = new MenuFlyoutItem
        {
            Text = "Delete Profile",
            Tag = profile
        };
        deleteItem.Click += (s, e) => DeleteProfileRequested?.Invoke(this, profile);
        flyout.Items.Add(deleteItem);

        return flyout;
    }

    private MenuFlyout BuildSubProfileContextFlyout(GameProfile profile, SubProfile subProfile)
    {
        var flyout = new MenuFlyout();
        var context = (profile, subProfile);

        var renameItem = new MenuFlyoutItem
        {
            Text = "Rename Sub-Profile",
            Tag = context
        };
        renameItem.Click += (s, e) => RenameSubProfileRequested?.Invoke(this, context);
        flyout.Items.Add(renameItem);

        var deleteItem = new MenuFlyoutItem
        {
            Text = "Delete Sub-Profile",
            Tag = context
        };
        deleteItem.Click += (s, e) => DeleteSubProfileRequested?.Invoke(this, context);
        flyout.Items.Add(deleteItem);

        return flyout;
    }

    /// <summary>Finds NavigationViewItem for profile using reference equality.</summary>
    public NavigationViewItem? FindProfileNavItem(GameProfile? profile)
    {
        if (profile == null)
        {
            return null;
        }

        foreach (var item in _navigationView.MenuItems)
        {
            if (item is NavigationViewItem navItem && ReferenceEquals(navItem.Tag, profile))
            {
                return navItem;
            }
        }

        return null;
    }

    /// <summary>Finds NavigationViewItem for sub-profile using reference equality.</summary>
    public NavigationViewItem? FindSubProfileNavItem(SubProfile? subProfile)
    {
        if (subProfile == null)
        {
            return null;
        }

        foreach (var item in _navigationView.MenuItems)
        {
            if (item is NavigationViewItem profileItem)
            {
                foreach (var child in profileItem.MenuItems)
                {
                    if (child is NavigationViewItem subItem && ReferenceEquals(subItem.Tag, subProfile))
                    {
                        return subItem;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>Synchronizes NavigationView selection with ProfileManagementService state.</summary>
    public void RestoreNavigationSelection()
    {
        NavigationViewItem? targetItem = FindSubProfileNavItem(_profileService.SelectedSubProfile);

        if (targetItem != null)
        {
            var owningProfile = _profileService.Profiles.FirstOrDefault(p => _profileService.SelectedSubProfile != null && p.SubProfiles.Contains(_profileService.SelectedSubProfile));
            var profileItem = FindProfileNavItem(owningProfile);
            if (profileItem != null)
            {
                profileItem.IsExpanded = true;
            }

            if (!ReferenceEquals(_navigationView.SelectedItem, targetItem))
            {
                // Set flag to prevent recursive selection change events
                _isRestoringSelection = true;
                try
                {
                    _navigationView.SelectedItem = targetItem;
                }
                finally
                {
                    _isRestoringSelection = false;
                }
            }
        }
        else
        {
            if (_profileService.SelectedProfile != null && _profileService.SelectedSubProfile == null)
            {
                var profileItem = FindProfileNavItem(_profileService.SelectedProfile);
                if (profileItem != null)
                {
                    profileItem.IsExpanded = true;
                }
            }

            if (_navigationView.SelectedItem != null)
            {
                _isRestoringSelection = true;
                try
                {
                    _navigationView.SelectedItem = null;
                }
                finally
                {
                    _isRestoringSelection = false;
                }
            }
        }
    }

    /// <summary>Subscribes to profile's sub-profile collection change events.</summary>
    public void HookSubProfileCollection(GameProfile profile)
    {
        profile.SubProfiles.CollectionChanged -= SubProfiles_CollectionChanged;
        profile.SubProfiles.CollectionChanged += SubProfiles_CollectionChanged;
    }

    private async void Profiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Suppress events during service refresh to prevent duplicate UI updates
        if (_profileService.IsRefreshing) return;

        bool profileRemoved = _profileService.SelectedProfile != null && !_profileService.Profiles.Contains(_profileService.SelectedProfile);
        bool subRemoved = _profileService.SelectedSubProfile != null && (_profileService.SelectedProfile == null || !_profileService.SelectedProfile.SubProfiles.Contains(_profileService.SelectedSubProfile));

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var profile in _profileService.Profiles)
            {
                HookSubProfileCollection(profile);
            }
        }
        else
        {
            if (e.OldItems != null)
            {
                foreach (GameProfile profile in e.OldItems)
                {
                    profile.SubProfiles.CollectionChanged -= SubProfiles_CollectionChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (GameProfile profile in e.NewItems)
                {
                    HookSubProfileCollection(profile);
                }
            }
        }

        await HandleSelectionValidation();
    }

    private async void SubProfiles_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_profileService.SelectedProfile != null && _profileService.SelectedSubProfile != null && !_profileService.SelectedProfile.SubProfiles.Contains(_profileService.SelectedSubProfile))
        {
            var fallback = _profileService.SelectedProfile.SubProfiles.FirstOrDefault();
            await _profileService.SelectProfileAsync(_profileService.SelectedProfile, fallback, activateInRust: true);
        }

        await HandleSelectionValidation();
    }

    /// <summary>Indicates whether selection is being restored programmatically.</summary>
    public bool IsRestoringSelection => _isRestoringSelection;

    /// <summary>Captures current expansion state of all profile items.</summary>
    public Dictionary<Guid, bool> CaptureExpansionStates()
    {
        var states = new Dictionary<Guid, bool>();

        foreach (var item in _navigationView.MenuItems)
        {
            if (item is NavigationViewItem navItem && navItem.Tag is GameProfile profile)
            {
                states[profile.Id] = navItem.IsExpanded;
            }
        }

        return states;
    }

    /// <summary>Rebuilds all navigation items and restores expansion states.</summary>
    public void RefreshNavigationItems(Dictionary<Guid, bool> expansionStates)
    {
        _navigationView.MenuItems.Clear();

        foreach (var profile in _profileService.Profiles)
        {
            var profileItem = CreateProfileNavigationItem(profile);

            if (expansionStates.TryGetValue(profile.Id, out bool wasExpanded))
            {
                profileItem.IsExpanded = wasExpanded;
            }

            _navigationView.MenuItems.Add(profileItem);
        }

        RestoreNavigationSelection();
    }

    private async Task HandleSelectionValidation()
    {
        var expansionStates = CaptureExpansionStates();
        RefreshNavigationRequested?.Invoke(expansionStates);

        if (_profileService.Profiles.Count == 0)
        {
            _profileService.SelectedProfile = null;
            _profileService.SelectedSubProfile = null;
            ClearMappingsRequested?.Invoke();
            UpdateSelectionHeaderRequested?.Invoke();
            UpdateMappingCountRequested?.Invoke();
            return;
        }

        bool profileRemoved = _profileService.SelectedProfile != null &&
            !_profileService.Profiles.Contains(_profileService.SelectedProfile);
        bool subRemoved = _profileService.SelectedSubProfile != null &&
            (_profileService.SelectedProfile == null ||
             !_profileService.SelectedProfile.SubProfiles.Contains(_profileService.SelectedSubProfile));

        if (profileRemoved)
        {
            var fallbackProfile = _profileService.Profiles.FirstOrDefault();
            if (fallbackProfile != null)
            {
                await _profileService.SelectProfileAsync(fallbackProfile,
                    fallbackProfile.SubProfiles.FirstOrDefault(), activateInRust: true);
            }
        }
        else if (subRemoved && _profileService.SelectedProfile != null)
        {
            await _profileService.SelectProfileAsync(_profileService.SelectedProfile,
                _profileService.SelectedProfile.SubProfiles.FirstOrDefault(), activateInRust: true);
        }
    }
}
