using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System.Linq;

namespace White_Desert.Controls;

public class PageContainer : SelectingItemsControl
{
    static PageContainer()
    {
        SelectionModeProperty.OverrideDefaultValue<PageContainer>(SelectionMode.AlwaysSelected);
        SelectedIndexProperty.OverrideMetadata<PageContainer>(new DirectPropertyMetadata<int>(0));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedIndexProperty)
        {
            UpdateChildVisibility();
        }
        
        if (change.Property == SelectedIndexProperty)
        {
            UpdateChildVisibility();
        }
    }

    private void UpdateChildVisibility()
    {
        if (LogicalChildren == null) return;

        var children = LogicalChildren.OfType<Control>().ToList();
        for (int i = 0; i < children.Count; i++)
        {
            children[i].IsVisible = (i == SelectedIndex);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        UpdateChildVisibility();
    }
}