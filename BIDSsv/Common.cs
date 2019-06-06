﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TR.BIDSSMemLib;

namespace TR.BIDSsv
{
  static public class UFunc
  {
    public static string Comp(object oldobj, object newobj) => Equals(oldobj, newobj) ? string.Empty : newobj.ToString();
  }

  static public class Common
  {
    static public readonly int Version = 202;
    static public readonly int DefPNum = 14147;


    static public BIDSSharedMemoryData BSMD
    {
      get => (BIDSSharedMemoryData)SML?.Read<BIDSSharedMemoryData>();
      set => SML?.Write(in value);
    }
    static public OpenD OD
    {
      get => (OpenD)SML?.Read<OpenD>();
      set => SML?.Write(in value);
    }
    static public PanelD PD
    {
      get => (PanelD)SML?.Read<PanelD>();
      set => SML?.Write(in value);
    }
    static public SoundD SD
    {
      get => (SoundD)SML?.Read<SoundD>();
      set => SML?.Write(in value);
    }

    static public Hand Ctrl_Hand
    {
      get => CI?.GetHandD() ?? new Hand();
      set => CI?.SetHandD(ref value);
    }
    static public bool[] Ctrl_Key
    {
      get => CI?.GetIsKeyPushed() ?? new bool[128];
      set => CI?.SetIsKeyPushed(in value);
    }

    static public int PowerNotchNum
    {
      get => Ctrl_Hand.P;
      set => CI?.SetHandD(CtrlInput.HandType.Power, value);
    }
    static public int BrakeNotchNum
    {
      get => Ctrl_Hand.B;
      set => CI?.SetHandD(CtrlInput.HandType.Brake, value);
    }
    static public int ReverserNum
    {
      get => Ctrl_Hand.R;
      set => CI?.SetHandD(CtrlInput.HandType.Reverser, value);
    }

    static public event EventHandler<SMemLib.BSMDChangedEArgs> BSMDChanged
    {
      add => SMemLib.BIDSSMemChanged += value;
      remove => SMemLib.BIDSSMemChanged -= value;
    }
    static public event EventHandler<SMemLib.OpenDChangedEArgs> OpenDChanged
    {
      add => SMemLib.OpenDChanged += value;
      remove => SMemLib.OpenDChanged -= value;
    }
    static public event EventHandler<SMemLib.ArrayDChangedEArgs> PanelDChanged
    {
      add => SMemLib.PanelDChanged += value;
      remove => SMemLib.PanelDChanged -= value;
    }
    static public event EventHandler<SMemLib.ArrayDChangedEArgs> SoundDChanged
    {
      add => SMemLib.SoundDChanged += value;
      remove => SMemLib.SoundDChanged -= value;
    }


    const int OpenDBias = 1000000;
    const int ElapDBias = 100000;
    const int DoorDBias = 10000;
    const int HandDBias = 1000;

    static private List<IBIDSsv> svlist = new List<IBIDSsv>();
    static private IDictionary<string, int> PDAutoList = default;
    static private IDictionary<string, int> SDAutoList = default;
    static private IDictionary<string, int> AutoNumL = default;
    static private SMemLib SML = null;
    static private CtrlInput CI = null;

    static private bool IsStarted = false;
    static private bool IsDebug = false;

    static public void Start(int Interval = 10)
    {
      if (!IsStarted)
      {
        for (int i = 1; i <= 9; i++) stateAllStr += ("X{" + i.ToString() + "}");
        SML = new SMemLib(true, 0);
        CI = new CtrlInput();
        SML?.ReadStart(5, Interval);

        BSMDChanged += Common_BSMDChanged;
        OpenDChanged += Common_OpenDChanged;
        PanelDChanged += Common_PanelDChanged;
        SoundDChanged += Common_SoundDChanged;
        IsStarted = true;
      }
    }
    
    private static void Common_SoundDChanged(object sender, SMemLib.ArrayDChangedEArgs e)
    {
      if (!IsStarted) return;
      if (svlist?.Count > 0) Parallel.For(0, svlist.Count, (i) => svlist[i].OnSoundDChanged(in e.NewArray));

      if (SDAutoList.Count > 0 && svlist.Count > 0)
      {
        Parallel.For(0, Math.Max(e.OldArray.Length, e.NewArray.Length), (i) =>
        {
          if (SDAutoList.Values.Contains(i))
          {
            int? Num = null;
            if (e.OldArray.Length <= i) Num = e.NewArray[i];
            else if (e.NewArray.Length > i && e.OldArray[i] != e.NewArray[i]) Num = e.NewArray[i];

            if (Num != null)
            {
              Parallel.For(0, svlist.Count, (s) =>
              {
                if (SDAutoList.Contains(new KeyValuePair<string, int>(svlist[s].Name, i)))
                  svlist[s].Print("TRIS" + i.ToString() + "X" + Num.ToString());
              });
            }
          }
        });
      }
    }
    private static void Common_PanelDChanged(object sender, SMemLib.ArrayDChangedEArgs e)
    {
      if (!IsStarted) return;
      if (svlist?.Count > 0) Parallel.For(0, svlist.Count, (i) => svlist[i].OnPanelDChanged(in e.NewArray));

      if (PDAutoList.Count > 0 && svlist.Count > 0)
      {
        Parallel.For(0, Math.Max(e.OldArray.Length, e.NewArray.Length), (i) =>
        {
          if (PDAutoList.Values.Contains(i))
          {
            int? Num = null;
            if (e.OldArray.Length <= i) Num = e.NewArray[i];
            else if (e.NewArray.Length > i && e.OldArray[i] != e.NewArray[i]) Num = e.NewArray[i];

            if (Num != null)
            {
              Parallel.For(0, svlist.Count, (s) =>
              {
                if (PDAutoList.Contains(new KeyValuePair<string, int>(svlist[s].Name, i)))
                  svlist[s].Print("TRIP" + i.ToString() + "X" + Num.ToString());
              });
            }
          }
        });
      }
    }
    private static void Common_OpenDChanged(object sender, SMemLib.OpenDChangedEArgs e) { if (!IsStarted) return; if (svlist?.Count > 0) Parallel.For(0, svlist.Count, (i) => svlist[i].OnOpenDChanged(in e.NewData)); }
    private static void Common_BSMDChanged(object sender, SMemLib.BSMDChangedEArgs e)
    {
      if (!IsStarted) return;
      if (svlist?.Count > 0) Parallel.For(0, svlist.Count, (i) => svlist[i].OnBSMDChanged(in e.NewData));

      if (AutoNumL?.Count > 0 && svlist?.Count > 0)
      {
        bool IsDClsdo = e.OldData.IsDoorClosed;
        bool IsDClsd = e.NewData.IsDoorClosed;
        Spec osp = e.OldData.SpecData;
        Spec nsp = e.NewData.SpecData;
        State ost = e.OldData.StateData;
        State nst = e.NewData.StateData;
        Hand oh = e.OldData.HandleData;
        Hand nh = e.NewData.HandleData;
        TimeSpan ots = TimeSpan.FromMilliseconds(e.OldData.StateData.T);
        TimeSpan nts = TimeSpan.FromMilliseconds(e.NewData.StateData.T);
        ICollection<int> IC = AutoNumL.Values;
        ICollection<int> ICR = default;
        Parallel.For(0, IC.Count, (ind) =>
        {
          int i = IC.ElementAt(ind);
          if (!ICR.Contains(i))
          {
            ICR.Add(i);

            string WriteStr = string.Empty;
            string chr = string.Empty;
            int num = 0;

            if (OpenDBias > i && i >= ElapDBias)
            {
              switch (i - ElapDBias)
              {
                case 0: WriteStr = UFunc.Comp(ost.Z, nst.Z); break;
                case 1: WriteStr = UFunc.Comp(ost.V, nst.V); break;
                case 2: WriteStr = UFunc.Comp(ost.T, nst.T); break;
                case 3: WriteStr = UFunc.Comp(ost.BC, nst.BC); break;
                case 4: WriteStr = UFunc.Comp(ost.MR, nst.MR); break;
                case 5: WriteStr = UFunc.Comp(ost.ER, nst.ER); break;
                case 6: WriteStr = UFunc.Comp(ost.BP, nst.BP); break;
                case 7: WriteStr = UFunc.Comp(ost.SAP, nst.SAP); break;
                case 8: WriteStr = UFunc.Comp(ost.I, nst.I); break;
                //case 9: WriteStr = UFunc.Comp(ost.Z, nst.Z); break;
                case 10: WriteStr = UFunc.Comp(ots.Hours, nts.Hours); break;
                case 11: WriteStr = UFunc.Comp(ots.Minutes, nts.Minutes); break;
                case 12: WriteStr = UFunc.Comp(ots.Seconds, nts.Seconds); break;
                case 13: WriteStr = UFunc.Comp(ots.Milliseconds, nts.Milliseconds); break;
              }
              (chr, num) = ("E", i - ElapDBias);
            }
            else if (i >= DoorDBias)
            {
              switch (i - DoorDBias)
              {
                case 0: WriteStr = UFunc.Comp(IsDClsdo ? 1 : 0, IsDClsd ? 1 : 0); break;
              }
              (chr, num) = ("D", i - DoorDBias);
            }
            else if (i >= HandDBias)
            {
              switch (i - HandDBias)
              {
                case 0: WriteStr = UFunc.Comp(oh.B, nh.B); break;
                case 1: WriteStr = UFunc.Comp(oh.P, nh.P); break;
                case 2: WriteStr = UFunc.Comp(oh.R, nh.R); break;
                case 3: WriteStr = UFunc.Comp(oh.C, nh.C); break;
              }
              (chr, num) = ("H", i - HandDBias);
            }
            else if (OpenDBias > i)
            {
              switch (i)
              {
                case 0: WriteStr = UFunc.Comp(osp.B, nsp.B); break;
                case 1: WriteStr = UFunc.Comp(osp.P, nsp.P); break;
                case 2: WriteStr = UFunc.Comp(osp.A, nsp.A); break;
                case 3: WriteStr = UFunc.Comp(osp.J, nsp.J); break;
                case 4: WriteStr = UFunc.Comp(osp.C, nsp.C); break;
              }
              (chr, num) = ("C", i % HandDBias);
            }


            if (WriteStr != string.Empty)
            {
              Parallel.For(0, svlist.Count, (s) =>
              {
                if (SDAutoList.Contains(new KeyValuePair<string, int>(svlist[s].Name, i)))
                  svlist[s].Print("TRIS" + num.ToString() + "X" + WriteStr);
              });
            }
          }
        });
      }
    }

    static public void Add<T>(ref T container) where T : IBIDSsv => svlist.Add(container);
    static public void Remove() => Remove(string.Empty);
    static public void Remove(in string Name)
    {
      if (Name != string.Empty)
      {
        try
        {
          if (PDAutoList?.Count > 0) PDAutoList.Remove(Name);
        }
        catch (Exception e)
        {
          Console.WriteLine(e);
        }
        try
        {
          if (SDAutoList?.Count > 0) SDAutoList.Remove(Name);
        }
        catch (Exception e)
        {
          Console.WriteLine(e);
        }
        try
        {
          if (AutoNumL?.Count > 0) AutoNumL.Remove(Name);
        }
        catch (Exception e)
        {
          Console.WriteLine(e);
        }
        for (int i = svlist.Count - 1; i >= 0; i--)
          if (Name == svlist[i].Name)
          {
            try
            {
              svlist[i].Dispose();
              svlist.RemoveAt(i);
            }
            catch (Exception e)
            {
              Console.WriteLine(e);
            }
          }
      }
      else
      {
        if (svlist.Count > 0) for (int i = svlist.Count - 1; i >= 0; i--)
          {
            svlist[i]?.Dispose();
            svlist?.RemoveAt(i);
          }
      }
    }
    static public void DebugDo() => DebugDo(string.Empty);
    static public void DebugDo(in string Name)
    {
      if (svlist.Count > 0)
      {
        Console.WriteLine("Debug Start");
        for (int i = 0; i < svlist.Count; i++)
          if (Name == string.Empty || Name == svlist[i].Name) svlist[i].IsDebug = true;
        IsDebug = Name == string.Empty;
        Console.ReadKey();
        IsDebug = false;
        for (int i = 0; i < svlist.Count; i++) svlist[i].IsDebug = false;
        Console.WriteLine("Debug End");
      }
      else Console.WriteLine("DebugDo : There are no connection.");
    }

    static public string PrintList()
    {
      string s = string.Empty;
      if (svlist?.Count > 0) for (int i = 0; i < svlist.Count; i++) s += i.ToString() + " : " + svlist[i].Name + "\n";
      else s = "There are no connection.\n";
      return s;
    }

    static public void Dispose()
    {
      if (svlist.Count > 0) Parallel.For(0, svlist.Count, (i) => svlist[i].Dispose());
      SML?.ReadStop();
      SML?.Dispose();
    }

    static string stateAllStr = "{0}";
    static public byte[] DataSelect(in string CName, in byte[] data, in Encoding enc)
    {
      if (data.Length < 5) return null;
      List<byte> dbl = data.ToList();
      for (int i = 0; i < dbl.Count; i++)
      {
        if (dbl[i] == '\r')
        {
          switch (dbl[i + 1])
          {
            case 0x01:
              dbl[i] = (byte)'\n';
              dbl.RemoveAt(i + 1);
              break;
            case 0x02:
              dbl.RemoveAt(i + 1);
              break;
          }
        }
      }
      byte[] ba = dbl.ToArray();
      if (ba[0] == (byte)'T')
      {
        switch ((char)ba[1])
        {
          case 'R':
            string sr = DataSelectTR(CName, enc.GetString(ba));
            if (sr != null && sr != string.Empty) return enc.GetBytes(sr);
            break;
          case 'O':
            string so = DataSelectTO(enc.GetString(ba));
            if (so != null && so != string.Empty) return enc.GetBytes(so);
            break;
        }
      }
      else if (ba[0] == 0x54 && ba[1] == 0x52) 
      {
        switch (ba[2])
        {
          case 0x62://Info Data
            switch (ba[3])
            {
              case 0x01://Spec
                if (Convert.ToUInt16(ba.Skip(4).Take(2)) >= Version) return null;
                else if (ba.Length >= 5 * 4) 
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  OpenD od = OD;
                  Spec s = bsmd.SpecData;
                  int i = 0;
                  s.B = Convert.ToUInt16(ba.Skip(i += 8).Take(2));
                  s.P = Convert.ToUInt16(ba.Skip(i += 2).Take(2));
                  s.A = Convert.ToUInt16(ba.Skip(i += 2).Take(2));
                  s.J = Convert.ToUInt16(ba.Skip(i += 2).Take(2));
                  s.C = Convert.ToUInt16(ba.Skip(i += 2).Take(2));
                  od.SelfBCount = Convert.ToUInt16(ba.Skip(i++).Take(2));
                  bsmd.SpecData = s;
                  BSMD = bsmd;
                  OD = od;
                }
                else return ba;
                break;
              case 0x02://State
                if (ba.Length >= 13 * 4)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  State s = bsmd.StateData;
                  int i = 0;
                  s.Z = Convert.ToDouble(ba.Skip(i += 4).Take(8));
                  s.V = Convert.ToSingle(ba.Skip(i += 8).Take(4));
                  s.I = Convert.ToSingle(ba.Skip(i += 4).Take(4));
                  //s. = Convert.ToSingle(ba.Skip(i += 4).Take(4));WireVoltage
                  s.BC = Convert.ToSingle(ba.Skip(i += 8).Take(4));
                  s.MR = Convert.ToSingle(ba.Skip(i += 4).Take(4));
                  s.ER = Convert.ToSingle(ba.Skip(i += 4).Take(4));
                  s.BP = Convert.ToSingle(ba.Skip(i += 4).Take(4));
                  s.SAP = Convert.ToSingle(ba.Skip(i += 4).Take(4));
                  bsmd.IsDoorClosed = (ba[13 * 4] & 0b10000000) > 0;
                  BSMD = bsmd;
                }
                else return ba;
                break;
              case 0x03://BVE5D
                break;
              case 0x04://OpenD
                break;

            }
            break;
          case 0x70://Panel Data
            break;
          case 0x73:
            break;
        }
      }
      return null;
    }
    static public string DataSelectTR(in string CName, in string GotString)
    {
      if (IsDebug) Console.Write("{0} << {1}", CName, GotString);
      string ReturnString = GotString.Replace("\n", string.Empty) + "X";

      //0 1 2 3
      //T R X X
      switch (GotString.Substring(2, 1))
      {
        case "R"://レバーサー
          switch (GotString.Substring(3))
          {
            case "R":
              ReverserNum = -1;
              break;
            case "N":
              ReverserNum = 0;
              break;
            case "F":
              ReverserNum = 1;
              break;
            case "-1":
              ReverserNum = -1;
              break;
            case "0":
              ReverserNum = 0;
              break;
            case "1":
              ReverserNum = 1;
              break;
            default:
              return "TRE7";//要求情報コードが不正
          }
          return ReturnString + "0";
        case "S"://ワンハンドル
          int sers = 0;
          try
          {
            sers = Convert.ToInt32(GotString.Substring(3));
          }
          catch (FormatException)
          {
            return "TRE6";//要求情報コード 文字混入
          }
          catch (OverflowException)
          {
            return "TRE5";//要求情報コード 変換オーバーフロー
          }
          int pnn = 0;
          int bnn = 0;
          if (sers > 0) pnn = sers;
          if (sers < 0) bnn = -sers;
          PowerNotchNum = pnn;
          BrakeNotchNum = bnn;
          return ReturnString + "0";
        case "P"://Pノッチ操作
          int serp = 0;
          try
          {
            serp = Convert.ToInt32(GotString.Substring(3));
          }
          catch (FormatException)
          {
            return "TRE6";//要求情報コード 文字混入
          }
          catch (OverflowException)
          {
            return "TRE5";//要求情報コード 変換オーバーフロー
          }
          PowerNotchNum = serp;
          return ReturnString + "0";
        case "B"://Bノッチ操作
          int serb = 0;
          try
          {
            serb = Convert.ToInt32(GotString.Substring(3));
          }
          catch (FormatException)
          {
            return "TRE6";//要求情報コード 文字混入
          }
          catch (OverflowException)
          {
            return "TRE5";//要求情報コード 変換オーバーフロー
          }
          BrakeNotchNum = serb;
          return ReturnString + "0";
        case "K"://キー操作
          int serk = 0;
          try
          {
            serk = Convert.ToInt32(GotString.Substring(4));
          }
          catch (FormatException)
          {
            return "TRE6";//要求情報コード 文字混入
          }
          catch (OverflowException)
          {
            return "TRE5";//要求情報コード 変換オーバーフロー
          }
          switch (GotString.Substring(3, 1))
          {
            //udpr
            case "U":
              //if (KyUp(serk)) return ReturnString + "0";
              //else return "TRE8";
              return "TRE3";
            case "D":
              //if (KyDown(serk)) return ReturnString + "0";
              //else return "TRE8";
              return "TRE3";
            case "P":
              if (serk < 128)
              {
                CI?.SetIsKeyPushed(serk, true);
                return ReturnString + "0";
              }
              else
              {
                return "TRE2";
              }
            case "R":
              if (serk < 128)
              {
                CI?.SetIsKeyPushed(serk, false);
                return ReturnString + "0";
              }
              else
              {
                return "TRE2";
              }
            default:
              return "TRE3";//記号部不正
          }
        case "I"://情報取得
          if (!BSMD.IsEnabled) return "TRE1";
          int seri = 0;
          try
          {
            seri = Convert.ToInt32(GotString.Substring(4));
          }
          catch (FormatException)
          {
            return "TRE6";//要求情報コード 文字混入
          }
          catch (OverflowException)
          {
            return "TRE5";//要求情報コード 変換オーバーフロー
          }
          switch (GotString.Substring(3, 1))
          {
            case "C":
              switch (seri)
              {
                case -1:
                  Spec spec = BSMD.SpecData;
                  return ReturnString + string.Format("{0}X{1}X{2}X{3}X{4}", spec.B, spec.P, spec.A, spec.J, spec.C);
                case 0:
                  return ReturnString + BSMD.SpecData.B.ToString();
                case 1:
                  return ReturnString + BSMD.SpecData.P.ToString();
                case 2:
                  return ReturnString + BSMD.SpecData.A.ToString();
                case 3:
                  return ReturnString + BSMD.SpecData.J.ToString();
                case 4:
                  return ReturnString + BSMD.SpecData.C.ToString();
                default: return "TRE2";
              }
            case "E":
              switch (seri)
              {
                case -3://Time
                  TimeSpan ts3 = TimeSpan.FromMilliseconds(BSMD.StateData.T);
                  return ReturnString + string.Format("{0}:{1}:{2}.{3}", ts3.Hours, ts3.Minutes, ts3.Seconds, ts3.Milliseconds);
                case -2://Pressure
                  State st2 = BSMD.StateData;
                  return ReturnString + string.Format("{0}X{1}X{2}X{3}X{4}", st2.BC, st2.MR, st2.ER, st2.BP, st2.SAP);
                case -1://All
                  State st1 = BSMD.StateData;
                  return ReturnString + string.Format(stateAllStr, st1.Z, st1.V, st1.T, st1.BC, st1.MR, st1.ER, st1.BP, st1.SAP, st1.I, 0);
                case 0: return ReturnString + BSMD.StateData.Z;
                case 1: return ReturnString + BSMD.StateData.V;
                case 2: return ReturnString + BSMD.StateData.T;
                case 3: return ReturnString + BSMD.StateData.BC;
                case 4: return ReturnString + BSMD.StateData.MR;
                case 5: return ReturnString + BSMD.StateData.ER;
                case 6: return ReturnString + BSMD.StateData.BP;
                case 7: return ReturnString + BSMD.StateData.SAP;
                case 8: return ReturnString + BSMD.StateData.I;
                //case 9: return ReturnString + BSMD.StateData.Volt;//予約 電圧
                case 10: return ReturnString + TimeSpan.FromMilliseconds(BSMD.StateData.T).Hours.ToString();
                case 11: return ReturnString + TimeSpan.FromMilliseconds(BSMD.StateData.T).Minutes.ToString();
                case 12: return ReturnString + TimeSpan.FromMilliseconds(BSMD.StateData.T).Seconds.ToString();
                case 13: return ReturnString + TimeSpan.FromMilliseconds(BSMD.StateData.T).Milliseconds.ToString();
                default: return "TRE2";
              }
            case "H":
              switch (seri)
              {
                case -1:
                  Hand hd1 = BSMD.HandleData;
                  return ReturnString + string.Format("{0}X{1}X{2}X{3}", hd1.B, hd1.P, hd1.R, hd1.C);
                case 0: return ReturnString + BSMD.HandleData.B;
                case 1: return ReturnString + BSMD.HandleData.P;
                case 2: return ReturnString + BSMD.HandleData.R;
                case 3: return ReturnString + BSMD.HandleData.C;//定速状態は予約
                case 4:
                  OpenD od = new OpenD();
                  SML?.Read(out od);
                  if (od.IsEnabled) return ReturnString + od.SelfBPosition.ToString();
                  else return "TRE1";//SMem is not connected.
                default: return "TRE2";
              }
            case "P":
              PanelD pd;
              SML?.Read(out pd);
              if (seri < 0) return ReturnString + pd.Length.ToString();
              else return ReturnString + (seri < pd.Length ? pd.Panels[seri] : 0).ToString();
            case "S":
              SoundD sd;
              SML?.Read(out sd);
              if (seri < 0) return ReturnString + sd.Length.ToString();
              else return ReturnString + (seri < sd.Length ? sd.Sounds[seri] : 0).ToString();
            case "D":
              switch (seri)
              {
                case 0: return ReturnString + (BSMD.IsDoorClosed ? "1" : "0");
                case 1: return ReturnString + "0";
                case 2: return ReturnString + "0";
                default: return "TRE2";
              }
            case "p":
              PanelD pda;
              SML?.Read(out pda);

              ReturnString += ((seri * 32) >= pda.Length) ? 0 : pda.Panels[seri * 32];
              for (int i = (seri * 32) + 1; i < (seri + 1) * 32; i++)
                ReturnString += "X" + ((i >= pda.Length) ? 0 : pda.Panels[i]);

              return ReturnString;
            case "s":
              SoundD sda;
              SML?.Read(out sda);
              ReturnString += ((seri * 32) >= sda.Length) ? 0 : sda.Sounds[seri * 32];
              for (int i = (seri * 32) + 1; i < (seri + 1) * 32; i++)
                ReturnString += "X" + ((i >= sda.Length) ? 0 : sda.Sounds[i]);

              return ReturnString;
            default: return "TRE3";//記号部不正
          }
        case "A"://Auto Send Add
          int sera = 0;
          try
          {
            sera = Convert.ToInt32(GotString.Substring(4));
          }
          catch (FormatException)
          {
            return "TRE6";//要求情報コード 文字混入
          }
          catch (OverflowException)
          {
            return "TRE5";//要求情報コード 変換オーバーフロー
          }

          int Bias = -1;
          switch (GotString.Substring(3, 1))
          {
            case "C":
              Bias = 0;
              break;
            case "H":
              Bias = HandDBias;
              break;
            case "D":
              Bias = DoorDBias;
              break;
            case "E":
              Bias = ElapDBias;
              break;
            case "P":
              if (!PDAutoList.Contains(new KeyValuePair<string, int>(CName, sera))) PDAutoList.Add(CName, sera);
              return ReturnString + "0";
            case "S":
              if (!SDAutoList.Contains(new KeyValuePair<string, int>(CName, sera))) SDAutoList.Add(CName, sera);
              return ReturnString + "0";
          }


          if (Bias >= 0)
          {
            if (!AutoNumL.Contains(new KeyValuePair<string, int>(CName, Bias + sera))) AutoNumL.Add(CName, Bias + sera);
            return ReturnString + "0";
          }
          else return "TRE3";
        case "D"://Auto Send Delete
          int Biasd = -1;
          int serd;
          try
          {
            serd = Convert.ToInt32(GotString.Substring(4));
          }
          catch (FormatException)
          {
            return "TRE6";//要求情報コード 文字混入
          }
          catch (OverflowException)
          {
            return "TRE5";//要求情報コード 変換オーバーフロー
          }

          switch (GotString.Substring(3, 1))
          {
            case "C":
              Biasd = 0;
              break;
            case "H":
              Biasd = HandDBias;
              break;
            case "D":
              Biasd = DoorDBias;
              break;
            case "E":
              Biasd = ElapDBias;
              break;
            case "P":
              if (PDAutoList.Contains(new KeyValuePair<string, int>(CName, serd))) PDAutoList.Remove(new KeyValuePair<string, int>(CName, serd));
              return ReturnString + "0";
            case "S":
              if (!SDAutoList.Contains(new KeyValuePair<string, int>(CName, serd))) SDAutoList.Remove(new KeyValuePair<string, int>(CName, serd));
              return ReturnString + "0";
          }

          if (Biasd > 0)
          {
            if (AutoNumL.Contains(new KeyValuePair<string, int>(CName, Biasd + serd))) AutoNumL.Remove(new KeyValuePair<string, int>(CName, Biasd + serd));
            return ReturnString + "0";
          }
          else return "TRE3";
        case "E":
        //throw new Exception(GotString);
        default:
          return "TRE4";//識別子不正
      }
    }
    static public string DataSelectTO(in string GotStr)
    {
      string GotString = GotStr.Replace("\n", string.Empty);
      string ThirdStr = GotString.Substring(2, 1);
      if (ThirdStr == "R")
      {
        switch (GotString.Substring(3, 1))
        {
          case "F":
            ReverserNum = 1;
            break;
          case "N":
            ReverserNum = 0;
            break;
          case "R":
            ReverserNum = -1;
            break;
          case "B":
            ReverserNum = -1;
            break;
        }
      }
      else if (ThirdStr == "K")
      {
        int KNum = 0;
        try
        {
          KNum = Convert.ToInt32(GotString.Substring(3).Replace("D", string.Empty).Replace("U", string.Empty));
        }
        catch (FormatException)
        {
          return "TRE6";//要求情報コード 文字混入
        }
        catch (OverflowException)
        {
          return "TRE5";//要求情報コード 変換オーバーフロー
        }
        if (GotString.EndsWith("D"))
        {
          CI?.SetIsKeyPushed(KNum, true);
        }
        if (GotString.EndsWith("U"))
        {
          CI?.SetIsKeyPushed(KNum, false);
        }
      }
      else
      {
        int Num = 0;
        try
        {
          Num = Convert.ToInt32(GotString.Substring(3));
        }
        catch (FormatException)
        {
          return "TRE6";//要求情報コード 文字混入
        }
        catch (OverflowException)
        {
          return "TRE5";//要求情報コード 変換オーバーフロー
        }
        switch (ThirdStr)
        {
          case "B":
            BrakeNotchNum = Num;
            break;
          case "P":
            PowerNotchNum = Num;
            break;
          case "H":
            PowerNotchNum = -Num;
            break;
        }
      }
      return GotString;
    }
    static public void DataGot(in string GotStr)
    {
      if (GotStr == null || GotStr == string.Empty) return;
      if (!GotStr.StartsWith("TR")) return;
      string[] GSA = GotStr.Replace("\n", string.Empty).Split('X');
      if (GSA[0].StartsWith("TRI"))
      {
        int seri = 0;
        try
        {
          seri = Convert.ToInt32(GotStr.Substring(4));
        }
        catch (FormatException)
        {
          throw new Exception("TRE6");//要求情報コード 文字混入
        }
        catch (OverflowException)
        {
          throw new Exception("TRE5");//要求情報コード 変換オーバーフロー
        }
        switch (GotStr.Substring(3, 1))
        {
          case "C":
            switch (seri)
            {
              case -1:
                if (GSA.Length > 5)
                {
                  Spec spec;
                  try
                  {
                    spec.B = int.Parse(GSA[1]);
                    spec.P = int.Parse(GSA[2]);
                    spec.A = int.Parse(GSA[3]);
                    spec.J = int.Parse(GSA[4]);
                    spec.C = int.Parse(GSA[5]);
                    BIDSSharedMemoryData bsmd = BSMD;
                    bsmd.SpecData = spec;
                    BSMD = bsmd;
                  }
                  catch (Exception) { throw; }
                }
                break;
              case 0:
                try
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.SpecData.B = int.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                catch (Exception) { throw; }
                break;
              case 1:
                try
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.SpecData.P = int.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                catch (Exception) { throw; }
                break;
              case 2:
                try
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.SpecData.A = int.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                catch (Exception) { throw; }
                break;
              case 3:
                try
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.SpecData.J = int.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                catch (Exception) { throw; }
                break;
              case 4:
                try
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.SpecData.C = int.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                catch (Exception) { throw; }
                break;
              default: throw new Exception("TRE2");
            }
            break;
          case "E":
            switch (seri)
            {
              case -3://Time
                if (GSA.Length > 4)
                {
                  TimeSpan ts3 = new TimeSpan(0, int.Parse(GSA[1]), int.Parse(GSA[2]), int.Parse(GSA[3]), int.Parse(GSA[4]));
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.StateData.T = (int)ts3.TotalMilliseconds;
                  BSMD = bsmd;
                }
                break;
              case -2://Pressure
                if (GSA.Length > 5)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  int i = 1;
                  bsmd.StateData.BC = float.Parse(GSA[i++]);
                  bsmd.StateData.MR = float.Parse(GSA[i++]);
                  bsmd.StateData.ER = float.Parse(GSA[i++]);
                  bsmd.StateData.BP = float.Parse(GSA[i++]);
                  bsmd.StateData.SAP = float.Parse(GSA[i++]);
                  BSMD = bsmd;
                }
                break;
              case -1://All
                if (GSA.Length > 10)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  State st = bsmd.StateData;
                  int i = 1;
                  st.Z = double.Parse(GSA[i++]);
                  st.V = float.Parse(GSA[i++]);
                  st.T = int.Parse(GSA[i++]);
                  st.BC = float.Parse(GSA[i++]);
                  st.MR = float.Parse(GSA[i++]);
                  st.ER = float.Parse(GSA[i++]);
                  st.BP = float.Parse(GSA[i++]);
                  st.SAP = float.Parse(GSA[i++]);
                  st.I = float.Parse(GSA[i++]);
                }
                break;
              case 0:
                if (GSA.Length >= 2)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.StateData.Z = double.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                break;
              case 1:
                if (GSA.Length >= 2)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.StateData.V = float.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                break;
              case 2:
                if (GSA.Length >= 2)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.StateData.T = int.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                break;
              case 3:
                if (GSA.Length >= 2)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.StateData.BC = float.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                break;
              case 4:
                if (GSA.Length >= 2)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.StateData.MR = float.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                break;
              case 5:
                if (GSA.Length >= 2)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.StateData.ER = float.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                break;
              case 6:
                if (GSA.Length >= 2)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.StateData.BP = float.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                break;
              case 7:
                if (GSA.Length >= 2)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.StateData.SAP = float.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                break;
              case 8:
                if (GSA.Length >= 2)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.StateData.I = float.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                break;
              //case 9: return ReturnString + BSMD.StateData.Volt;//予約 電圧
              case 10://Hour
                break;
              case 11:
                break;
              case 12:
                break;
              case 13:
                break;
              default: throw new Exception("TRE2");
            }
            break;
          case "H":
            switch (seri)
            {
              case -1:
                if (GSA.Length > 4)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.HandleData.B = int.Parse(GSA[1]);
                  bsmd.HandleData.P = int.Parse(GSA[2]);
                  bsmd.HandleData.R = int.Parse(GSA[3]);
                  bsmd.HandleData.C = int.Parse(GSA[4]);
                  BSMD = bsmd;
                }
                break;
              case 0:
                if (GSA.Length > 1)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.HandleData.B = int.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                break;
              case 1:
                if (GSA.Length > 1)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.HandleData.P = int.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                break;
              case 2:
                if (GSA.Length > 1)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.HandleData.R = int.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                break;
              case 3:
                if (GSA.Length > 1)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.HandleData.C = int.Parse(GSA[1]);
                  BSMD = bsmd;
                }
                break;
              case 4:
                if (GSA.Length > 1)
                {
                  OpenD opd = OD;
                  opd.SelfBPosition = int.Parse(GSA[1]);
                  OD = opd;
                }
                break;
              default: break;
            }
            break;
          case "P":
            if (GSA.Length > 1 && seri >= 0 && seri < PD.Length)
              PD.Panels[seri] = int.Parse(GSA[1]);
            break;
          case "S":
            if (GSA.Length > 1 && seri >= 0 && seri < SD.Length)
              SD.Sounds[seri] = int.Parse(GSA[1]);
            break;
          case "D":
            switch (seri)
            {
              case 0:
                if (GSA.Length > 1)
                {
                  BIDSSharedMemoryData bsmd = BSMD;
                  bsmd.IsDoorClosed = GSA[1] == "1";
                  BSMD = bsmd;
                }
                break;
              default: break;
            }
            break;
          case "p":
            if (GSA.Length > 32 && seri >= 0)
            {
              int mx = (seri + 1) * 32;
              int[] pda;
              if (PD.Length >= mx)
              {
                pda = new int[mx];
                Array.Copy(PD.Panels, pda, PD.Length);
              }
              else pda = PD.Panels;

              for (int i = seri * 32; i < mx; i++)
                if (i < PD.Length) pda[i] = int.Parse(GSA[(i % 32) + 1]);
              PanelD pd = new PanelD() { Panels = pda };
              PD = pd;
            }
            break;
          case "s":
            if (GSA.Length > 32 && seri >= 0)
            {
              int mx = (seri + 1) * 32;
              int[] sda;
              if (SD.Length >= mx)
              {
                sda = new int[mx];
                Array.Copy(SD.Sounds, sda, SD.Length);
              }
              else sda = SD.Sounds;

              for (int i = seri * 32; i < mx; i++)
                if (i < SD.Length) sda[i] = int.Parse(GSA[(i % 32) + 1]);
              SoundD sd = new SoundD() { Sounds = sda };
              SD = sd;
            }
            break;
          default: throw new Exception("TRE3");//記号部不正
        }
      }
    }
  }
}