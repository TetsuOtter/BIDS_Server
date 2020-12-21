using System;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Text;

namespace TR.BIDSsv
{
  public class Serial : IBIDSsv
  {
    public bool IsDisposed { get; private set; } = false;
    public int Version { get; set; } = 202;
    public bool IsDebug { get => sdc.IsDebugging; set => sdc.IsDebugging = value; }
    public string Name { get; private set; } = "serial";

    public bool ReConnectWhenTimedOut { get; private set; } = false;
    public bool AliveCMD { get; private set; } = false;

    Serial_DeviceCom sdc = null;
    private bool IsBinaryAllowed = false;

    SerialPort SP = new SerialPort();
    public bool Connect(in string args)
    {
      if (SP.IsOpen)
        SP.Close();//開いてるなら, 閉じてから

      SP.BaudRate = 19200;
      SP.RtsEnable = true;
      SP.DtrEnable = true;
      SP.ReadTimeout = 20;
      SP.WriteTimeout = 500;
      SP.Encoding = Encoding.ASCII;
      SP.NewLine = "\n";
      string[] sa = args.Replace(" ", string.Empty).Split(new string[2] { "-", "/" }, StringSplitOptions.RemoveEmptyEntries);
      for (int i = 0; i < sa.Length; i++)
      {
        string[] saa = sa[i].Split(':');
        if (saa.Length > 0)
        {
          try
          {
            switch (saa[0])
            {
              case "B":
                SP.BaudRate = int.Parse(saa[1]);
                break;
              case "BaudRate":
                SP.BaudRate = int.Parse(saa[1]);
                break;
              case "BS":
                IsBinaryAllowed = true;
                break;
              case "BinarySender":
                IsBinaryAllowed = true;
                break;
              case "DataBits":
                SP.DataBits = int.Parse(saa[1]);
                break;
              case "DTR":
                SP.DtrEnable = saa[1] == "1";
                break;
              case "E":
                switch (int.Parse(saa[1]))
                {
                  case 0:
                    SP.Encoding = Encoding.Default;
                    break;
                  case 1:
                    SP.Encoding = Encoding.ASCII;
                    break;
                  case 2:
                    SP.Encoding = Encoding.Unicode;
                    break;
                  case 3:
                    SP.Encoding = Encoding.UTF8;
                    break;
                  case 4:
                    SP.Encoding = Encoding.UTF32;
                    break;
                  default:
                    SP.Encoding = Encoding.Default;
                    break;
                }
                break;
              case "Encoding":
                switch (int.Parse(saa[1]))
                {
                  case 0:
                    SP.Encoding = Encoding.Default;
                    break;
                  case 1:
                    SP.Encoding = Encoding.ASCII;
                    break;
                  case 2:
                    SP.Encoding = Encoding.Unicode;
                    break;
                  case 3:
                    SP.Encoding = Encoding.UTF8;
                    break;
                  case 4:
                    SP.Encoding = Encoding.UTF32;
                    break;
                  default:
                    SP.Encoding = Encoding.Default;
                    break;
                }
                break;
              case "HandShake":
                SP.Handshake = (Handshake)int.Parse(saa[1]);
                break;
              case "N":
                Name = saa[1];
                break;
              case "Name":
                Name = saa[1];
                break;
              case "NL":
                switch (int.Parse(saa[1]))
                {
                  case 1:
                    SP.NewLine = "\r";
                    break;
                  case 2:
                    SP.NewLine = "\r\n";
                    break;
                  default:
                    SP.NewLine = "\n";
                    break;
                }
                break;
              case "NewLine":
                switch (int.Parse(saa[1]))
                {
                  case 1:
                    SP.NewLine = "\r";
                    break;
                  case 2:
                    SP.NewLine = "\r\n";
                    break;
                  default:
                    SP.NewLine = "\n";
                    break;
                }
                break;
              case "P":
                try
                {
                  SP.PortName = "COM" + int.Parse(saa[1]);
                }
                catch (FormatException)
                {
                  SP.PortName = saa[1];
                }
                break;
              case "Port":
                try
                {
                  SP.PortName = "COM" + int.Parse(saa[1]);
                }
                catch (FormatException)
                {
                  SP.PortName = saa[1];
                }
                break;
              case "Parity":
                SP.Parity = (Parity)int.Parse(saa[1]);
                break;
              case "RTO":
                SP.ReadTimeout = int.Parse(saa[1]);
                break;
              case "ReadTimeout":
                SP.ReadTimeout = int.Parse(saa[1]);
                break;
              case "RTS":
                SP.RtsEnable = saa[1] == "1";
                break;
              case "RCWTO":
                ReConnectWhenTimedOut = saa[1] == "1";
                break;
              case "StopBits":
                SP.StopBits = (StopBits)int.Parse(saa[1]);
                break;
              case "WTO":
                SP.WriteTimeout = int.Parse(saa[1]);
                break;
              case "WriteTimeout":
                SP.WriteTimeout = int.Parse(saa[1]);
                break;
              case "ALVCHK":
                AliveCMD = saa[1] == "1";
                break;
            }
          }catch(Exception e)
          {
            Console.WriteLine("arg({0}) Error : {1}", sa[i], e);
            continue;
          }
        }
      }
      if (sdc is null)
      {
        try
        {
          sdc = new Serial_DeviceCom(SP);

          Console.WriteLine(Name + " : " + sdc.IsOpen);

          sdc.StringDataReceived += Sdc_StringDataReceived;
          sdc.BinaryDataReceived += Sdc_BinaryDataReceived;


        }
        catch (Exception e)
        {
          Console.WriteLine("{0} : an exception has occured in the init section.\n{1}", Name, e);
          return false;
        }
      }
      else
        sdc.ReConnect();//再度の呼び出し時には再接続扱いとする

      sdc.ReConnectWhenTimedOut = ReConnectWhenTimedOut;
      sdc.IamAliveCMD = AliveCMD;
      return sdc.IsOpen;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]//関数のインライン展開を積極的にやってもらう.
    private void Sdc_BinaryDataReceived(object sender, byte[] e) => Common.DataSelSend(this, e, sdc.Enc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]//関数のインライン展開を積極的にやってもらう.
    private void Sdc_StringDataReceived(object sender, string e) => Common.DataSelSend(this, e);

    public void OnBSMDChanged(in BIDSSharedMemoryData data) { }
    public void OnOpenDChanged(in OpenD data) { }
    public void OnPanelDChanged(in int[] data) { }
    public void OnSoundDChanged(in int[] data) { }


    readonly string[] ArgInfo = new string[]
    {
      "Argument Format ... Header(\"-\" or \"/\") + SettingName(B, P etc...) + [Separater(\":\") + Setting(38400, 2 etc...)]",
      "  -B or -BaudRate : Set BaudRate.  If you don\'t set this command, the BaudRate is set to 19200.",
      "  -BS or -BinarySender : Allow to send the binary data.",
      "  -DataBits : Set the DataBit Count Option.  Default:8  If you want More info about this argument, please read the source code.",
      "  -DTR : Set the RTS (Data Terminal Ready) Signal Option.  Default:1  0:Disabled, 1:Enabled",
      "  -E or -Encoding : Set the Encoding Option.  Default:0  If you want More info about this argument, please read the source code.",
      "  -HandShake : Set the HandShake Option.  Default:0  If you want More info about this argument, please read the source code.",
      "  -N or -Name : Set the Instance Name.  Default:\"serial\"  If you don't set this option, this program maybe cause some bugs.",
      "  -NL or -NewLine : Set the NewLine Char.  Default:0 (0:\\n, 1:\\r, 2:\\r\\n)",
      "  -P or -PortName : Set PortName.  You must set this option.  \"COM\" is unneeded in the Setting.  Only Number is allowed in the setting.",
      "  -Parity : Set the Parity Option.  Default:0  If you want More info about this argument, please read the source code.",
      "  -RTO or ReadTimeout : Set the ReadTimeout time option.  Default:100",
      "  -RTS : Set the RTS (Request to Send) Signal Option.  Default:1  0:Disabled, 1:Enabled",
      "  -StopBits : Set the StopBits Option.  Default:0  If you want More info about this argument, please read the source code.",
      "  -WTO or WriteTimeOut : Set the WriteTimeout time option  Default:1000",
      "  -ALVCHK : 生存確認のために, 1秒間隔でNULL文字を送出します.  1で有効",
      "  -RCWTO : 通信がタイムアウトした場合に, 自動再接続機能を使用するかどうかの設定です.  1で有効"
    };
    public void WriteHelp(in string args)
    {
      Console.WriteLine("BIDS Server Program Serial Interface");
      Console.WriteLine("Version : " + Version.ToString());
      Console.WriteLine("Copyright (C) Tetsu Otter 2019");
      foreach (string s in ArgInfo) Console.WriteLine(s);

    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]//関数のインライン展開を積極的にやってもらう.
    public void Print(in string data) => sdc?.PrintString(data);

    /// <summary>(IsBinaryAllowedがtrueの場合のみ)Binaryデータを送信します.</summary>
    /// <param name="data">送信するデータ</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]//関数のインライン展開を積極的にやってもらう.
    public void Print(in byte[] data)
    {
      if (IsBinaryAllowed) sdc?.PrintBinary(data);
    }


    #region IDisposable Support
    private bool disposedValue = false; // 重複する呼び出しを検出するには

    protected virtual void Dispose(bool disposing)
    {
      if (!disposedValue)
      {
        if (disposing)
        {
          // TODO: マネージ状態を破棄します (マネージ オブジェクト)。
        }

        // TODO: アンマネージ リソース (アンマネージ オブジェクト) を解放し、下のファイナライザーをオーバーライドします。
        // TODO: 大きなフィールドを null に設定します。

        sdc?.Dispose();

        disposedValue = true;
      }
    }

    // TODO: 上の Dispose(bool disposing) にアンマネージ リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします。
    // ~Serial()
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
