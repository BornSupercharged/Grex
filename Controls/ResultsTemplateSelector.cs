using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Grex.Models;

namespace Grex.Controls
{
    public class ResultsTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ContentTemplate { get; set; }
        public DataTemplate? FilesTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is FileSearchResult)
            {
                return FilesTemplate;
            }
            else if (item is SearchResult)
            {
                return ContentTemplate;
            }
            
            return base.SelectTemplateCore(item, container);
        }
    }
}
