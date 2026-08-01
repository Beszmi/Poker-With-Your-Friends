using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;

namespace Poker_With_Your_Friends.View;

public sealed class GameMenuItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate TableTemplate { get; set; } = null!;
    public DataTemplate AddTableTemplate { get; set; } = null!;

    protected override DataTemplate SelectTemplateCore(object item)
    {
        return item switch
        {
            Table => TableTemplate,
            AddTableMenuItem => AddTableTemplate,
            _ => base.SelectTemplateCore(item)
        };
    }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}
