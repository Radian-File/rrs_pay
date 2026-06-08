using System;
using System.Windows;
using rrs_pay.Data;
using rrs_pay.Services;
using rrs_pay.ViewModels;

namespace rrs_pay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var initializer = new DatabaseInitializer();
                await initializer.InitializeAsync();
                _viewModel.SetStatus($"Database ready • {AppDbContext.DefaultDatabasePath}");
            }
            catch (Exception ex)
            {
                _viewModel.SetStatus("Database initialization failed. See error dialog.");
                MessageBox.Show(
                    this,
                    $"RRS Pay could not initialize the local SQLite database.\n\n{ex.Message}",
                    "Database initialization failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
