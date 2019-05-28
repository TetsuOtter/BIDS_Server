﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using TR.BIDScs;
using TR.BIDSSMemLib;
using TR.BIDSsv;

namespace BIDS_Server
{
  class Program
  {
    /*BIDS Server
     * HTML+JavaScriptとの間でWebSocketでの通信。
     */
     
    static SMemLib SML = null;
    static CtrlInput CI = null;
    static bool IsLooping = true;
    const string VerNumStr = "011a";
    static void Main(string[] args)
    {
      Console.WriteLine("BIDS Server Application");
      Console.WriteLine("ver : " + VerNumStr);
      Console.WriteLine("In this version(" + VerNumStr + "), only Serial Connection is supported.");
      //SocketDo();
      SMemLib.BIDSSMemChanged += SMemLib_BIDSSMemChanged;
      SMemLib.OpenDChanged += SMemLib_OpenDChanged;
      SMemLib.PanelDChanged += SMemLib_PanelDChanged;
      SMemLib.SoundDChanged += SMemLib_SoundDChanged;
      SML = new SMemLib(true, 0);
      CI = new CtrlInput();
      do ReadLineDO(); while (IsLooping);
      Console.WriteLine("Please press any key to exit...");
      Console.ReadKey();
    }

    private static void SMemLib_SoundDChanged(object sender, SMemLib.ArrayDChangedEArgs e) => Parallel.For(0, svlist.Count, (i) => svlist[i]?.OnSoundDChanged(in e.NewArray));
    private static void SMemLib_PanelDChanged(object sender, SMemLib.ArrayDChangedEArgs e) => Parallel.For(0, svlist.Count, (i) => svlist[i]?.OnPanelDChanged(in e.NewArray));
    private static void SMemLib_OpenDChanged(object sender, SMemLib.OpenDChangedEArgs e) => Parallel.For(0, svlist.Count, (i) => svlist[i]?.OnOpenDChanged(in e.NewData));
    private static void SMemLib_BIDSSMemChanged(object sender, SMemLib.BSMDChangedEArgs e) => Parallel.For(0, svlist.Count, (i) => svlist[i]?.OnBSMDChanged(in e.NewData));

    static List<IBIDSsv> svlist = new List<IBIDSsv>();

    static void ReadLineDO()
    {
      string s = Console.ReadLine();
      string[] cmd = s.ToLower().Split(' ');
      switch (cmd[0])
      {
        case "man":
          if (cmd.Length >= 2)
          {
            switch (cmd[1])
            {
              case "serial":
                (new Serial()).WriteHelp(in s);
                break;
              case "exit":
                Console.WriteLine("exit command : Used when user want to close this program.  This command has no arguments.");
                break;
              case "ls":
                Console.WriteLine("connection listing command : Print the list of the Name of alive connection.  This command has no arguments.");
                break;
              case "close":
                Console.WriteLine("close connection command : Used when user want to close the connection.  User must set the Connection Name to be closed.");
                break;
              default:
                Console.WriteLine("Command not found.");
                break;
            }
          }
          else
          {
            Console.WriteLine("BIDS Server Application");
            Console.WriteLine("ver : " + VerNumStr + "\n");
            Console.WriteLine("close: Close the connection.");
            Console.WriteLine("exit : close this program.");
            Console.WriteLine("ls : Print the list of the Name of alive connection.");
            Console.WriteLine("man : Print the mannual of command.");
            Console.WriteLine("serial : Start the serial connection.  If you want to know more info, please do the command \"man serial\".");
          }
          break;
        case "serial":
          Serial sl = new Serial();
          sl.KeyCtrl += KeyCtrl;
          sl.HandleCtrl += HandleCtrl;
          try
          {
            IBIDSsvSetUp(ref sl, in s);
            svlist.Add(sl);
          }
          catch (Exception e)
          {
            Console.WriteLine(e);
            sl.Dispose();
          }
          break;
        case "ls":
          if (svlist.Count > 0) for (int i = 0; i < svlist.Count; i++) Console.WriteLine(i.ToString() + " : " + svlist[i].Name);
          else Console.WriteLine("No connection is alive.");
          break;
        case "exit":
          for (int i = 0; i < svlist.Count; i++) IBIDSsvDispose(i);
          IsLooping = false;
          break;
        case "close":
          if (svlist.Count > 0)
          {
            for (int i = 0; i < svlist.Count; i++)
            {
              if (svlist[i].Name == cmd[1])
              {
                svlist[i].Dispose();
                svlist.RemoveAt(i);
              }
            }
          }
          else Console.WriteLine("No connection is alive.");
          break;
        case "debug":
          if (svlist.Count > 0)
          {
            for (int i = 0; i < svlist.Count; i++)
            {
              if (cmd.Length <= 1 || svlist[i].Name == cmd[1])
              {
                Console.WriteLine("debug start");
                svlist[i].IsDebug = true;
                Console.ReadKey();
                svlist[i].IsDebug = false;
                Console.WriteLine("debug end");
              }
            }
          }
          else Console.WriteLine("No connection is alive.");
          break;
        default:
          Console.WriteLine("Command Not Found");
          break;
      }
    }


    private static void IBIDSsvDispose(int ind)
    {
      svlist[ind].BSMDChanged -= BSMDChanged;
      svlist[ind].HandleCtrl -= HandleCtrl;
      svlist[ind].KeyCtrl -= KeyCtrl;
      svlist[ind].OpenDChanged -= OpenDChanged;
      svlist[ind].PanelDChanged -= PanelDChanged;
      svlist[ind].SoundDChanged -= SoundDChanged;
      svlist[ind].Dispose();
    }
    private static void IBIDSsvSetUp<T>(ref T sv, in string s) where T : IBIDSsv
    {
      sv.Connect(in s);
      sv.BSMDChanged += BSMDChanged;
      sv.HandleCtrl += HandleCtrl;
      sv.KeyCtrl += KeyCtrl;
      sv.OpenDChanged += OpenDChanged;
      sv.PanelDChanged += PanelDChanged;
      sv.SoundDChanged += SoundDChanged;
    }

    private static void SoundDChanged(object sender, EventArgs e)
    {
      throw new NotImplementedException();
    }

    private static void PanelDChanged(object sender, EventArgs e)
    {
      throw new NotImplementedException();
    }

    private static void OpenDChanged(object sender, EventArgs e)
    {
      throw new NotImplementedException();
    }

    private static void KeyCtrl(object sender, KeyCtrlEvArgs e)
    {
      bool[] ks = CI.GetIsKeyPushed();
      Parallel.For(0, ks.Length, (i) => ks[i] = e.KeyState[i] ?? ks[i]);
      CI.SetIsKeyPushed(ks);
    }

    private static void HandleCtrl(object sender, HandleCtrlEvArgs e)
    {
      Hand hd = CI.GetHandD();
      hd.B = e.Brake ?? hd.B;
      hd.P = e.Power ?? hd.P;
      hd.R = e.Reverser ?? hd.R;
      CI.SetHandD(ref hd);
    }

    private static void BSMDChanged(object sender, EventArgs e)
    {
      throw new NotImplementedException();
    }

    static async void SocketDo()
    {
      HttpListener HL = new HttpListener();
      HL.Prefixes.Add("http://localhost:12835/");
      HL.Start();
      while (true)
      {
        HttpListenerContext HLC = await HL.GetContextAsync();
        if (HLC.Request.IsWebSocketRequest)
        {
          Console.WriteLine("WebSocket接続要求を確認しました。");
          WebSocketContext WSC = null;
          try
          {
            WSC = await HLC.AcceptWebSocketAsync(subProtocol: null);
          }catch(Exception e)
          {
            Console.WriteLine("Socket開始エラー : " + e.Message);
            HLC.Response.StatusCode = 500;
            HLC.Response.Close();
            Console.WriteLine("再度接続試行を行います。");
          }
          Console.WriteLine("WebSocket接続を開始します。");
          WebSocket WS = WSC.WebSocket;
          try
          {
            using (Pipe Pp = new Pipe())
            {
              Pp.Open();
              byte[] SendArray = new byte[64];
              Pipe.StateData ST1 = new Pipe.StateData();
              Pipe.State2Data ST2 = new Pipe.State2Data();
              while (WS.State == WebSocketState.Open)
              {
                if (!Equals(ST1, Pp.NowState) || !Equals(ST2, Pp.NowState2))
                {
                  ST1 = Pp.NowState;
                  ST2 = Pp.NowState2;
                  BitConverter.GetBytes(2).CopyTo(SendArray, 0);
                  BitConverter.GetBytes(ST1.Location).CopyTo(SendArray, 4);
                  BitConverter.GetBytes(ST1.Speed).CopyTo(SendArray, 12);
                  BitConverter.GetBytes(ST2.BC).CopyTo(SendArray, 16);
                  BitConverter.GetBytes(ST2.MR).CopyTo(SendArray, 20);
                  BitConverter.GetBytes(ST2.ER).CopyTo(SendArray, 24);
                  BitConverter.GetBytes(ST2.BP).CopyTo(SendArray, 28);
                  BitConverter.GetBytes(ST2.SAP).CopyTo(SendArray, 32);
                  BitConverter.GetBytes(ST1.Current).CopyTo(SendArray, 36);
                  BitConverter.GetBytes(ST1.PowerNotch).CopyTo(SendArray, 40);
                  BitConverter.GetBytes(ST1.BrakeNotch).CopyTo(SendArray, 44);
                  BitConverter.GetBytes((sbyte)ST1.Reverser).CopyTo(SendArray, 48);
                  BitConverter.GetBytes(ST2.IsDoorClosed).CopyTo(SendArray, 49);
                  BitConverter.GetBytes((byte)ST2.Hour).CopyTo(SendArray, 50);
                  BitConverter.GetBytes((byte)ST2.Minute).CopyTo(SendArray, 51);
                  BitConverter.GetBytes((byte)ST2.Second).CopyTo(SendArray, 52);
                  BitConverter.GetBytes((byte)ST2.Millisecond).CopyTo(SendArray, 53);
                  BitConverter.GetBytes(0xFEFEFEFE).CopyTo(SendArray, 60);
                  await WS.SendAsync(new ArraySegment<byte>(SendArray, 0, SendArray.Length), WebSocketMessageType.Binary, true, CancellationToken.None);
                }
                Thread.Sleep(10);
              }
            }
          }catch(Exception e)
          {
            Console.WriteLine("Socket通信エラー : " + e.Message);
            HLC.Response.StatusCode = 500;
            HLC.Response.Close();
          }
          finally { if (WS != null) WS.Dispose(); }
          Console.WriteLine("WebSocket接続が終了しました。再度接続を試行します。");
        }
        else
        {
          Console.WriteLine("WebSocket接続ではありませんでした。");
          HLC.Response.StatusCode = 400;
          HLC.Response.Close();
        }
      }
    }
  }
}