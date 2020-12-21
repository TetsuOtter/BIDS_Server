using System.Windows;
using System.Windows.Forms;

using Mackoy.Bvets;

namespace TR.BIDSsv
{
	public class bvets_id : IInputDevice
	{
		public event InputEventHandler LeverMoved;
		public event InputEventHandler KeyDown;
		public event InputEventHandler KeyUp;

		SettingWindow win = new SettingWindow();

		public void Configure(IWin32Window owner) => win.Show();


		public void Dispose() => win.Dispose();

		public void Load(string settingsPath) { }

		public void SetAxisRanges(int[][] ranges) { }

		public void Tick() { }
	}
}
