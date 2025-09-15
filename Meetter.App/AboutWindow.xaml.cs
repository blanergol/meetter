using System.Windows;

namespace Meetter.App;

public partial class AboutWindow : Window
{
	public AboutWindow()
	{
		InitializeComponent();
	}

	private void OnOk(object sender, RoutedEventArgs e) => Close();
}

