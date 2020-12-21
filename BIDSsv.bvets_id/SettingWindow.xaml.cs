using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;

namespace TR.BIDSsv
{
  /// <summary>
  /// SettingWindow.xaml の相互作用ロジック
  /// </summary>
  public partial class SettingWindow : Window, IDisposable
	{
		Serial serial = new Serial();
		SettingWindow_Data sw_d;
		static readonly string path = Assembly.GetExecutingAssembly().Location + ".setting";
		public SettingWindow()
		{
			InitializeComponent();
			Common.Add(ref serial);
			sw_d = new SettingWindow_Data();
			if (File.Exists(path))
				sw_d.CMD = File.ReadAllLines(path)[0];

			DataContext = sw_d;
		}

		private void ReConnectButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				serial.Connect(sw_d.CMD);
			}
			catch(Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		private void CloseButton_Click(object sender, RoutedEventArgs e)//With Save
		{
			File.WriteAllText(path, sw_d.CMD);
			Close();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)//With NoSave
		{
			Close();
		}

		public void Dispose() => ((IDisposable)serial).Dispose();
		
	}
	class SettingWindow_Data : INotifyPropertyChanged
	{
		public SettingWindow_Data()
		{
			BIDSSMemLib.SMemLib.Begin();
			BIDSSMemLib.SMemLib.SMC_BSMDChanged += SMemLib_SMC_BSMDChanged;
		}

		bool _IsBSMDEnabled = false;
		private void SMemLib_SMC_BSMDChanged(object sender, ValueChangedEventArgs<BIDSSharedMemoryData> e)
		{
			if (_IsBSMDEnabled != e.NewValue.IsEnabled)
			{
				_IsBSMDEnabled = e.NewValue.IsEnabled;
				IsBIDSConnected = _IsBSMDEnabled ? Brushes.Lime : Brushes.Red;
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		string _cmd = string.Empty;
		public string CMD { get => _cmd;
			set
			{
				_cmd = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CMD)));
			}
		}

		private Brush _IsBIDSConnected = Brushes.Red;
		public Brush IsBIDSConnected
		{
			get => _IsBIDSConnected;
			private set
			{
				_IsBIDSConnected = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBIDSConnected)));
			}
		}
	}
}
