using System;
using System.IO.Ports;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TR.BIDSsv
{
	/// <summary>
	/// デバイスとの通信を司る.
	/// </summary>
	internal class Serial_DeviceCom : IDisposable
	{
		#region 使用する変数類
		/// <summary>Serial IFが疎通しているかどうか</summary>
		public bool IsOpen { get => serial?.IsOpen == true; }

		/// <summary>デバッグ用出力を有効にするかどうか</summary>
		public bool IsDebugging { get; set; }

		public event EventHandler<string> StringDataReceived;
		public event EventHandler<byte[]> BinaryDataReceived;

		public Encoding Enc { get => serial?.Encoding ?? Encoding.ASCII; }

		public bool ReConnectWhenTimedOut { get; set; } = false;
		/// <summary>1秒の間隔をあけて, 生存報告をNULL文字送出にて行う.</summary>
		public bool IamAliveCMD { get; set; } = false;
		private Task AliveCMDTask = null;
		private readonly TimeSpan AliveCMDTimeSpan = new TimeSpan(0, 0, 1);
		private readonly TimeSpan ReConnectTimeSpan = new TimeSpan(0, 0, 0, 0, 100);

		private const string BINARY_DATA_HEADER = "B64E";
		private const string SERIAL_SETTING_HEADER = "S";

		object Locker = new object();

		string ReadBuf = string.Empty;
		/// <summary>使用するSerial IF</summary>
		SerialPort serial = null;
		#endregion

		/// <summary>インスタンスを初期化します.</summary>
		/// <param name="ser">使用するシリアルインターフェイス</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]//関数のインライン展開を積極的にやってもらう.
		public Serial_DeviceCom(in SerialPort ser)
		{
			if (ser == null) throw new ArgumentNullException();

			serial = ser;

			try
			{
				lock (Locker)
				{
					serial.Open();
				}
			}catch(Exception e)
			{
				Console.WriteLine("Serial_DeviceCom intialize : Serial Port Open Error=>{0}", e);
				return;
			}

			if (IamAliveCMD)
			{
				byte[] NULL_BA = new byte[1] { 0x00 };
				AliveCMDTask = new Task(async () =>
					{
						while (!disposingValue) {
							lock (Locker)
							{
								try
								{
									serial.Write(NULL_BA, 0, 1);
								}
								catch (TimeoutException e)
								{
									if (ReConnectWhenTimedOut)
									{
										ReConnect();
										continue;
									}
									else
									{
										Console.WriteLine("Serial_DeviceCom.AliveCMDCheck : {0}", e);
									}
								}
								catch(Exception e)
								{
									Console.WriteLine("Serial_DeviceCom.AliveCMDCheck : {0}", e);
									return;
								}
							}
							await Task.Delay(AliveCMDTimeSpan);
						}
					});//生存確認パケット送出用
			}

			serial.DataReceived += Serial_DataReceived;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]//関数のインライン展開を積極的にやってもらう.
		private void Serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			string gotData = string.Empty;
			SerialPort sp = (SerialPort)sender;
			try
			{
				if (!sp.IsOpen) Console.WriteLine("Serial_DeviceCom.Serial_DataReceived => Serial Already Closed.");
				if (!sp.BaseStream.CanRead || !sp.BaseStream.CanWrite) Console.WriteLine("Serial_DeviceCom.Serial_DataReceived : CanRead:{0}, CanWrite:{1}", sp.BaseStream.CanRead, sp.BaseStream.CanWrite);
				lock (Locker)
				{
					try
					{
						gotData = sp.ReadExisting();
					}
					catch (InvalidOperationException)
					{
						if (ReConnectWhenTimedOut) ReConnect();
					}
				}
			}catch(Exception ex)
			{
				Console.WriteLine("Serial_DeviceCom.Serial_DataReceived ReadExisting : {0}", ex);
			}
			if (string.IsNullOrWhiteSpace(gotData)) return;//要素なし
			Task.Run( async() =>
			{

				if (IsDebugging) Console.WriteLine("Serial_DeviceCom.Serial_DataReceived() : DataGot=>{0}", gotData);
				string[] sa = null;
				try
				{
					sa = (ReadBuf + gotData).Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Serial_DeviceCom.Serial_DataReceived StringSplit : {0}", ex);
				}
				if (!(sa?.Length > 0)) return;//要素なし

				int sa_len = sa.Length;
				if (gotData.EndsWith("\n") || gotData.EndsWith("\r")) ReadBuf = string.Empty;
				else
				{
					//最後の要素が改行記号で終わらない場合, その要素は次に判定を行う.
					ReadBuf = sa.Last() ?? string.Empty;
					sa_len -= 1;
				}

				if (sa_len <= 0) return;//要素なし

				for (int i = 0; i < sa_len; i++)
				{
					if (string.IsNullOrWhiteSpace(sa[i])) continue;//空要素は無視
					int ind = i;
					if (sa[ind].StartsWith(BINARY_DATA_HEADER))
						_ = Task.Run(() =>
							{
								try
								{
									BinaryDataReceived?.Invoke(this, Convert.FromBase64String(sa[ind].Substring(BINARY_DATA_HEADER.Length)));
								}
								catch (Exception ex)
								{
									Console.WriteLine("Serial_DataCom.Serial_DataReceived() BinaryDataReceieved Error : {0}", ex);
								}
							});
					else if (sa[i].StartsWith(SERIAL_SETTING_HEADER)) await Task.Run(() => Serial_Setting(sa[ind]));
					else _ = Task.Run(() =>
					{
						try
						{
							StringDataReceived?.Invoke(this, sa[ind]);
						}
						catch (Exception ex)
						{
							Console.WriteLine("Serial_DataCom.Serial_DataReceived() StringDatReceived.Invoke Error : {0}", ex);
						}
					});
				}
			});
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]//関数のインライン展開を積極的にやってもらう.
		void Serial_Setting(string cmd)
		{

		}

		#region 出力メソッド
		/// <summary>文字列を出力</summary>
		/// <param name="s">出力する文字列</param>
		/// <returns>出力に成功したかどうか</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]//関数のインライン展開を積極的にやってもらう.
		public bool PrintString(string s)
		{
			if (!IsOpen) return false;//null or openしてないなら実行しない.
			if (string.IsNullOrWhiteSpace(s)) return false;
			try
			{
				if (IsDebugging) Console.WriteLine("Serial_DeviceCom.PrintString() : DataSend=>{0}", s);
				lock (Locker)
				{
					try
					{
						serial.WriteLine(s);
					}
					catch (TimeoutException)
					{
						Console.WriteLine("\tTimeOut ({0})", s);
						if (ReConnectWhenTimedOut)
						{
							ReConnect();
							return PrintString(s);
						}
						return false;
					}
				}
				return true;
			}catch(Exception e)
			{
				Console.WriteLine("Serial_DeviceCom.PrintString({0}) : {1}", s, e);
				return false;
			}
		}

		/// <summary>Byte Arrayを適切な形で出力する.</summary>
		/// <param name="ba">出力するByte Array</param>
		/// <param name="offset">出力開始位置(nullで既定値"0")</param>
		/// <param name="length">出力するByte Arrayの長さ(nullで既定値"ba.length")</param>
		/// <returns>出力に成功したかどうか</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]//関数のインライン展開を積極的にやってもらう.
		public bool PrintBinary(byte[] ba, int? offset = null, int? length = null)
		{
			if (!IsOpen) return false;//null or openしてないなら実行しない.
			if (!(ba?.Length > 0)) return false;//0以下の要素 or nullなら実行しない.
			try
			{
				return PrintString(BINARY_DATA_HEADER + Convert.ToBase64String(ba, offset ?? 0, length ?? ba.Length, Base64FormattingOptions.None));//PrintString側でデバッグ用出力を実施
			}catch(Exception e)
			{
				Console.WriteLine("Serial_DeviceCom.PrintBinary() : {0}", e);
				return false;
			}
		}
		#endregion


		/// <summary>ロックは呼び出し元で取得してください.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]//関数のインライン展開を積極的にやってもらう.
		public async void ReConnect()
		{
			Console.WriteLine("Reconnect doing...");
			try
			{
				if (serial?.IsOpen == true) serial.Close();
			}
			catch (Exception e)
			{
				Console.WriteLine("SerialPort Close Failed. : {0}", e);
				return;
			}

			await Task.Delay(ReConnectTimeSpan);

			try
			{
				if (serial?.IsOpen == false) serial.Open();
			}
			catch (Exception e)
			{
				Console.WriteLine("SerialPort ReOpen Failed. : {0}", e);
				return;
			}
		}

		#region IDisposable Support
		private bool disposedValue = false; // 重複する呼び出しを検出するには
		private bool disposingValue = false;

		protected virtual void Dispose(bool disposing)
		{
			disposingValue = true;
			if (!disposedValue)
			{
				if (disposing)
				{
					// TODO: マネージ状態を破棄します (マネージ オブジェクト)。
				}

				// TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
				// TODO: 大きなフィールドを null に設定します。
				lock (Locker)
				{
					serial?.Dispose();
				}
				disposedValue = true;
			}
		}

		// TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
		// ~Serial_DeviceCom()
		// {
		//   // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
		//   Dispose(false);
		// }

		// このコードは、破棄可能なパターンを正しく実装できるように追加されました。
		public void Dispose()
		{
			// このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
			Dispose(true);
			// TODO: 上のファイナライザーがオーバーライドされる場合は、次の行のコメントを解除してください。
			// GC.SuppressFinalize(this);
		}
		#endregion

	}
}
