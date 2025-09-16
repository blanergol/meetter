using System.Windows;

namespace Meetter.App;

public partial class AboutWindow : Window
{
	public AboutWindow()
	{
		InitializeComponent();
		Icon = IconHelper.CreateWindowIcon();
	}

	private void OnOk(object sender, RoutedEventArgs e) => Close();
}

