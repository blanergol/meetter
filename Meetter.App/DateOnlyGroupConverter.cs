using System;
using System.Globalization;
using System.Windows.Data;

namespace Meetter.App;

public sealed class DateOnlyGroupConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is DateTimeOffset dto)
		{
			var date = dto.Date;
			var today = DateTimeOffset.Now.Date;
			return date == today ? "Сегодня" : date.ToString("dddd, dd.MM.yyyy", culture);
		}
		return "";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

