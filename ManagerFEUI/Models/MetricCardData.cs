using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ManagerFEUI.Models
{
    public class MetricCardData : INotifyPropertyChanged
    {
        private string _title = "";
        private double _percentage;
        private string _displayValue = "--";
        private string _sub = "";
        private string _color = "#2dd4a8";

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public double Percentage
        {
            get => _percentage;
            set { _percentage = value; OnPropertyChanged(); }
        }

        public string DisplayValue
        {
            get => _displayValue;
            set { _displayValue = value; OnPropertyChanged(); }
        }

        public string Sub
        {
            get => _sub;
            set { _sub = value; OnPropertyChanged(); }
        }

        public string Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        public MetricCardData() { }

        public MetricCardData(string title, double percentage, string displayValue, string sub, string color)
        {
            Title = title;
            Percentage = percentage;
            DisplayValue = displayValue;
            Sub = sub;
            Color = color;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}