using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace RetailPOS.Desktop.Converters;

public sealed class ProductCategoryImageConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var file = value?.ToString() switch
        {
            "Skin Care" => "skin-care.png",
            "Sun Care" => "sun-care.png",
            "Hair Care" => "hair-care.png",
            "Makeup" => "makeup.png",
            "Health" => "health.png",
            _ => "generic-product.png"
        };
        return new BitmapImage(new Uri($"pack://application:,,,/Assets/Products/{file}"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
