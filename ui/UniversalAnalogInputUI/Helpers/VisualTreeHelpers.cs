using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace UniversalAnalogInputUI.Helpers;

/// <summary>Traverses the WinUI 3 visual tree to find UI elements.</summary>
public static class VisualTreeHelpers
{
    /// <summary>Recursively searches downward to find child element of specific type.</summary>
    public static T? FindChildOfType<T>(DependencyObject parent, string name = "") where T : FrameworkElement
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild && (string.IsNullOrEmpty(name) || typedChild.Name == name))
                return typedChild;

            var result = FindChildOfType<T>(child, name);
            if (result != null)
                return result;
        }
        return null;
    }

    /// <summary>Walks upward to find parent element of specific type.</summary>
    public static T? FindParentOfType<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject parent = VisualTreeHelper.GetParent(child);
        while (parent != null)
        {
            if (parent is T typedParent)
                return typedParent;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
}
