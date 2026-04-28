using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace ManagerFEUI.Views
{
    public partial class PlaceholderPage : UserControl, INotifyPropertyChanged
    {
        private string _title = "";
        private string _subtitle = "";

        public string PlaceholderTitle
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public string PlaceholderSubtitle
        {
            get => _subtitle;
            set { _subtitle = value; OnPropertyChanged(); }
        }

        public PlaceholderPage(string title = "Coming Soon")
        {
            InitializeComponent();
            PlaceholderTitle = title;
            PlaceholderSubtitle = $"This module will be available in a future update.";
            DataContext = this;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}