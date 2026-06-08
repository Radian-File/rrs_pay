using System.Windows;
using System.Windows.Controls;

namespace rrs_pay.Views.Pages;

public partial class PlaceholderPage : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(PlaceholderPage),
        new PropertyMetadata("Module"));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(PlaceholderPage),
        new PropertyMetadata("Feature content will appear here."));

    public PlaceholderPage()
    {
        InitializeComponent();
    }

    public PlaceholderPage(string title, string description)
        : this()
    {
        Title = title;
        Description = description;
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}
