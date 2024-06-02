﻿using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace JDLibManager
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        /* 说明：
         * 命名为了整齐，尽量把每个单词缩写为3个字母，每个单词首字母大写
         * Lib指Rime的用户文件夹，或词库所在的文件夹，Wrd指词组，Dan指单字
         * Set, Add, Del, Exc, Edi, Log分别指六个功能页面
         */

        #region 全局变量

        private readonly HashSet<char> YinEle = new("bcdefghjklmnpqrstwxyz");//键道的音码码元
        private readonly HashSet<char> XngEle = new("aiouv");//键道的形码码元
        private readonly HashSet<char> AllEle = new("abcdefghijklmnopqrstuvwxyz");//键道的所有码元
        private const string ChnPat = @"[^\u4e00-\u9fff]";//用于正则匹配的中文模式
        private string WrdLoc = string.Empty;//词组文件位置
        private string DanLoc = string.Empty;//单字文件位置
        private readonly List<string> LstHed = new(5);//词组的文件头
        private SortedDictionary<string, List<string>> DicWrd = new();//词组，键是码
        private readonly Dictionary<string, HashSet<string>> DicDan = new();//单字，键是字，char装不下

        #endregion

        #region 用于编辑的方法

        private void DicWrdAdd(string AddWrd, string AddCod)//往词组里加东西
        {
            if (DicWrd.ContainsKey(AddCod))//如果有这一码
            {
                DicWrd[AddCod].Add(AddWrd);//在这码上加一词
                return;
            }
            DicWrd.Add(AddCod, new List<string> { AddWrd });//如果没这一码
        }

        private void DicWrdDel(string DelWrd, string DelCod)//在词组里删东西
        {
            _ = DicWrd[DelCod].Count == 1 ? DicWrd.Remove(DelCod) : DicWrd[DelCod].Remove(DelWrd);
        }

        private void DicDanAdd(string AddDan, string AddCod)//往单字里加东西
        {
            if (DicDan.ContainsKey(AddDan))//如果有这一字
            {
                _ = DicDan[AddDan].Add(AddCod);//在这字上加一码
                return;
            }
            DicDan.Add(AddDan, new HashSet<string> { AddCod });//如果没这一字
        }

        private static void LoadCodToCBB(HashSet<string> AllCod, ref ComboBox CBB)//往某个复选框中填放一组编码
        {
            CBB.ItemsSource = AllCod.Count == 1 ? AllCod : AllCod.OrderBy(x => x);
            CBB.SelectedIndex = 0;//自动选中第一个
        }

        private static bool CutWrd(ref string ProWrd)//*除去*指定字符串中不参与编码的字符，返回可用性
        {
            ProWrd = Regex.Replace(ProWrd, ChnPat, string.Empty);//除去非中文字符
            if (ProWrd.Length < 2)
                return false;
            if (ProWrd.Length > 4)
                ProWrd = ProWrd[..3] + ProWrd[^1];//只取前3字和末字
            return true;
        }

        private bool AllDan(string ProWrd)//检查指定字符串中的字是否全在单字中
        {
            return !ProWrd.Any(cha => !DicDan.ContainsKey(cha.ToString()));
        }

        private IEnumerable<string> GetAllFulCod(string ProWrd)//接收一个待编码的词组，返回无序、有重的所有全码
        {
            List<HashSet<string>> SubDan = ProWrd.Select(cha => DicDan[cha.ToString()]).ToList();//每个字的所有编码
            return ProWrd.Length switch
            {
                2 => from Dan1 in SubDan[0]
                     from Dan2 in SubDan[1]
                     select new string(new[] { Dan1[0], Dan1[1], Dan2[0], Dan2[1], Dan1[2], Dan2[2] }),

                3 => from Dan1 in SubDan[0]
                     from Dan2 in SubDan[1]
                     from Dan3 in SubDan[2]
                     select new string(new[] { Dan1[0], Dan2[0], Dan3[0], Dan1[2], Dan2[2], Dan3[2] }),

                _ => from Dan1 in SubDan[0]
                     from Dan2 in SubDan[1]
                     from Dan3 in SubDan[2]
                     from Dan4 in SubDan[3]
                     select new string(new[] { Dan1[0], Dan2[0], Dan3[0], Dan4[0], Dan1[2], Dan2[2] })
            };
        }

        private IEnumerable<string> TryGetAllCod(string ProWrd)//接收一个词组，返回词库中现有的所有码
        {
            return DicWrd.Where(onecode => onecode.Value.Contains(ProWrd)).Select(onecode => onecode.Key);
        }

        private string GetLngthnCod(string ProWrd, string ShoCod)//将输入的词组加长，返回最优的编码
        {
            List<string> LngthnCod = GetAllFulCod(ProWrd).Distinct()
                                                         .Where(x => x.StartsWith(ShoCod))
                                                         .ToList();

            if (LngthnCod.Count != 1)
                return string.Empty;

            for (int i = ShoCod.Length + 1; i < 6; i++)
            {
                if (!HavCod(LngthnCod[0][..i]))
                    return LngthnCod[0][..i];
            }

            return LngthnCod[0];
        }

        private bool WrdCodMch(string ProWrd, string ProCod)//检查词和码是否匹配
        {
            return GetAllFulCod(ProWrd).Any(x => x.StartsWith(ProCod));//若存在以该码开头的全码，则返回真
        }

        private bool HavWrdCod(string ProWrd, string ProCod)//检查词库中是否已有该项
        {
            if (HavCod(ProCod))
            {
                return DicWrd[ProCod].Contains(ProWrd);
            }
            return false;//若存在该项，则返回真
        }

        private bool HavWrd(string ProWrd)//检查词库中是否存在该词
        {
            return DicWrd.Any(x => x.Value.Contains(ProWrd));//若存在该词，则返回真
        }

        private bool HavCod(string ProCod)//检查该码是否已被占用
        {
            return DicWrd.ContainsKey(ProCod);//若已被占用，则返回真
        }

        private bool HavLonCod(string ProCod)//检查更长的码是否存在（删除该词造成空码）
        {
            return DicWrd.Any(x => x.Key.StartsWith(ProCod) && x.Key.Length > ProCod.Length);
        }

        private bool FulShoCod(string ProCod)//检查更短的码是否全被占用
        {
            //若此码长度大于3，且末码是形码，且依次拿掉形码，发现有空码，则返回假
            while (ProCod.Length > 3 && XngEle.Contains(ProCod[^1]))
            {
                ProCod = ProCod.Remove(ProCod.Length - 1);
                if (!HavCod(ProCod))
                    return false;
            }
            return true;
        }

        private bool TryWriWrd()//试图写入词库
        {
            try
            {
                using StreamWriter StrWriWrd = new(WrdLoc, false);//覆写
                foreach (string hed in LstHed)
                    StrWriWrd.WriteLine(hed);//写入文件头
                foreach (var (cod, wrds) in DicWrd)
                {
                    foreach (string wrd in wrds)
                    {
                        StrWriWrd.WriteLine(wrd + '\t' + cod);//写入词库
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show("写入词库出错: " + ex.Message,
                                    "错误",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                return false;
            }
        }

        private void WriTabLog(byte Pag)//把指定页面的日志写到日志页
        {
            switch (Pag)
            {
                case 1:
                    if (CBIgnAdd.IsChecked == true)//记录不带风险信息的日志
                    {
                        TBLog.Text += $"{TBAddWrd.Text}\t{CBAddCod.Text}\t{Pag}\t忽略风险\r\n";
                    }
                    else//记录带风险信息的日志
                    {
                        TBLog.Text += $"{TBAddWrd.Text}\t{CBAddCod.Text}\t{Pag}\t{GetRskInf(Pag)}\r\n";
                    }
                    break;
                case 2:
                    if (CBIgnDel.IsChecked == true)//记录不带风险信息的日志
                    {
                        TBLog.Text += $"{TBDelWrd.Text}\t{CBDelCod.Text}\t{Pag}\t忽略风险\r\n";
                    }
                    else//记录带风险信息的日志
                    {
                        TBLog.Text += $"{TBDelWrd.Text}\t{CBDelCod.Text}\t{Pag}\t{GetRskInf(Pag)}\r\n";
                    }
                    break;
                case 3:
                    if (CBIgnExc.IsChecked == true)//记录不带风险信息的日志
                    {
                        TBLog.Text += $"\t{CBExcCodLon.Text}\t{Pag}\t忽略风险\r\n";
                        TBLog.Text += $"{TBExcWrdLon.Text}\t{CBExcCodSho.Text}\t\t\r\n";
                        TBLog.Text += $"{TBExcWrdSho.Text}\t{LngthnCod}\t\t\r\n";
                    }
                    else//记录带风险信息的日志
                    {
                        TBLog.Text += $"\t{CBExcCodLon.Text}\t{Pag}\t{GetRskInf(Pag)}\r\n";
                        TBLog.Text += $"{TBExcWrdLon.Text}\t{CBExcCodSho.Text}\t\t\r\n";
                        TBLog.Text += $"{TBExcWrdSho.Text}\t{LngthnCod}\t\t\r\n";
                    }
                    break;
                case 4:
                    if (CBIgnEdi.IsChecked == true)//记录不带风险信息的日志
                    {
                        if (SliEdiWrdCod.Value == 0)//改码
                        {
                            TBLog.Text += $"{TBEdiWrdBef.Text}\t{TBEdiCodAft.Text}\t{Pag}\t忽略风险\r\n";
                            TBLog.Text += $"\t原码：{CBEdiCodBef.Text}\t\t\r\n";
                        }
                        else//改词
                        {
                            TBLog.Text += $"{TBEdiWrdAft.Text}\t{CBEdiCodBef.Text}\t{Pag}\t忽略风险\r\n";
                            TBLog.Text += $"原词：{TBEdiWrdBef.Text}\t\t\t\r\n";
                        }
                    }
                    else//记录带风险信息的日志
                    {
                        if (SliEdiWrdCod.Value == 0)//改码
                        {
                            TBLog.Text += $"{TBEdiWrdBef.Text}\t{TBEdiCodAft.Text}\t{Pag}\t{GetRskInf(Pag)}\r\n";
                            TBLog.Text += $"\t原码：{CBEdiCodBef.Text}\t\t\r\n";
                        }
                        else//改词
                        {
                            TBLog.Text += $"{TBEdiWrdAft.Text}\t{CBEdiCodBef.Text}\t{Pag}\t{GetRskInf(Pag)}\r\n";
                            TBLog.Text += $"原词：{TBEdiWrdBef.Text}\t\t\t\r\n";
                        }
                    }
                    break;
            }
        }

        private string GetRskInf(byte Pag)//获取指定页的风险提示代号
        {
            string RskInf = string.Empty;
            Dictionary<byte, List<CheckBox>> AllWarCBs = new()//每页的编号和对应的风险提示
            {
                { 1, new List<CheckBox> { WarAddWFZ, WarAddCZG, WarAddGDK, WarAddMWB, WarAddDMK, WarAddMBP } },
                { 2, new List<CheckBox> { WarDelDMK, WarDelSHL } },
                { 3, new List<CheckBox> { WarExcMWB, WarExcDMK, WarExcTHL } },
                { 4, new List<CheckBox> { WarEdiGDK, WarEdiMWB, WarEdiRYC, WarEdiGHL, WarEdiMBP } }
            };
            if (AllWarCBs.TryGetValue(Pag, out List<CheckBox>? WarCBs))//某页上的风险提示
            {
                foreach (CheckBox CB in WarCBs)//有该风险，则为1；无该风险，则为0
                {
                    RskInf = CB.IsChecked == true ? RskInf + "1" : RskInf + "0";
                }
            }
            return RskInf;
        }

        #endregion

        #region Set页的操作

        private bool BacOut;//是否已备份成功，初始为否
        private bool LogOut;//是否已导出日志，初始为否
        private const string MsgBoxLoaTip = "目录中的Rime键道词库。\r\n词库只载入一次，如有需要，请重新选择词库。\r\n所有修改都将覆写原词库，建议先备份再修改。";//提示性信息

        private bool TryReaWrd()//试图读取词组
        {
            DicWrd.Clear();//清空之前的读取
            LstHed.Clear();
            string? line;//一行
            string[] SplSli;//把一行分割得到的切片

            try
            {
                using StreamReader StrReaWrd = new(WrdLoc);
                while ((line = StrReaWrd.ReadLine()) != null)//读一行
                {
                    SplSli = line.Split('\t');//把这行分割
                    if (SplSli.Length == 2)//该行中有一个Tab，则载入并打断循环
                    {
                        DicWrdAdd(SplSli[0], SplSli[1]);
                        break;
                    }
                    LstHed.Add(line);//不是条目，则载入文件头
                }
                while ((line = StrReaWrd.ReadLine()) != null)//读一行
                {
                    SplSli = line.Split('\t');//把这行分割
                    if (SplSli.Length != 2)//格式不正确，则立即中止
                    {
                        ReaLibErr();
                        return false;
                    }
                    DicWrdAdd(SplSli[0], SplSli[1]);//载入
                }
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"读取词组出错: {ex.Message}",
                                    "错误",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                return false;
            }

            //读取完成，读空或含有无效码报错
            if (DicWrd.Count > 0 && WrdCodVal())
                return true;
            ReaLibErr();
            return false;
        }

        private bool WrdCodVal()//判断词组里是否每个编码的每个字符都有效
        {
            return !DicWrd.Keys.Any(onecod
                => onecod.Any(oneele
                => !AllEle.Contains(oneele)));
        }

        private bool TryReaDan()//试图读取单字
        {
            DicDan.Clear();//清空之前的读取
            string? line;//一行
            string[] SplSli;//把一行分割得到的切片

            try
            {
                using StreamReader StrReaDan = new(DanLoc);
                while ((line = StrReaDan.ReadLine()) != null)//读一行
                {
                    SplSli = line.Split('\t');//把这行分割
                    if (SplSli.Length == 2 && SplSli[1].Length > 2)//该行中有一个Tab且大于2码，则载入并打断循环
                    {
                        DicDanAdd(SplSli[0], SplSli[1]);
                        break;
                    }
                }
                while ((line = StrReaDan.ReadLine()) != null)//读一行
                {
                    SplSli = line.Split('\t');//把这行分割
                    if (SplSli.Length != 2)//格式不正确，则立即中止
                    {
                        ReaLibErr();
                        return false;
                    }
                    if (SplSli[1].Length > 2)//大于2码，则载入
                    {
                        DicDanAdd(SplSli[0], SplSli[1]);
                    }
                }
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"读取单字出错: {ex.Message}",
                                    "错误",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                return false;
            }

            //读取完成，读空或含有无效码报错
            if (DicDan.Count > 0 && DanCodVal())
                return true;
            ReaLibErr();
            return false;
        }

        private bool DanCodVal()//判断单字里是否每个字的每个编码都符合条件
        {
            return !DicDan.Values.Any(codcol
                => codcol.Any(onecod
                => !YinEle.Contains(onecod[0])
                   || !YinEle.Contains(onecod[1])
                   || !XngEle.Contains(onecod[2])));
        }

        private void ReaLibErr()//读取词库出错
        {
            DicWrd.Clear();
            DicDan.Clear();
            LstHed.Clear();
            _ = MessageBox.Show("词库加载出错！请检查词库中有无异常的行，然后重新选择。",
                                "错误",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
        }

        private bool TryLoaLib()//试图载入词组和单字
        {
            //若这两个文件都存在，且都加载成功，则返回真
            return File.Exists(WrdLoc)
                   && File.Exists(DanLoc)
                   && TryReaWrd()
                   && TryReaDan();
        }

        private void WinDowMai_Loaded(object sender, RoutedEventArgs e)//打开时自动加载词库
        {
            //程序上级目录
            WrdLoc = Path.GetFullPath(@"..\xkjd6.cizu.dict.yaml");
            DanLoc = Path.GetFullPath(@"..\xkjd6.danzi.dict.yaml");
            if (TryLoaLib())
            {
                _ = MessageBox.Show($"已自动载入程序上级{MsgBoxLoaTip}",
                                    "提示",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                TBLibLoc.Text = Path.GetDirectoryName(WrdLoc);
                return;
            }

            //Rime默认目录
            WrdLoc = $@"C:\Users\{Environment.UserName}\AppData\Roaming\Rime\xkjd6.cizu.dict.yaml";
            DanLoc = $@"C:\Users\{Environment.UserName}\AppData\Roaming\Rime\xkjd6.danzi.dict.yaml";
            if (TryLoaLib())
            {
                _ = MessageBox.Show($"已自动载入Rime默认{MsgBoxLoaTip}",
                                    "提示",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                TBLibLoc.Text = Path.GetDirectoryName(WrdLoc);
                return;
            }

            //都没有
            _ = MessageBox.Show("未能自动载入Rime键道词库。\r\n建议将此程序放在词库的下级目录中，或手动选择词库位置。",
                                "提示",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
        }

        private static string SetLibLoc()//选择词库位置
        {
            OpenFileDialog SetLoc = new()
            {
                DefaultExt = ".yaml",
                InitialDirectory = $@"C:\Users\{Environment.UserName}\AppData\Roaming\Rime",
                Filter = "Rime键道词库 (.yaml)|*.yaml",
                Title = "请选取xkjd6.cizu.dict.yaml文件"
            };
            return SetLoc.ShowDialog() == true ? SetLoc.FileName : string.Empty;
        }

        private void ButSetLibLoc_Click(object sender, RoutedEventArgs e)//手动加载词库
        {
            string tmploc = SetLibLoc();
            if (tmploc.Length > 0)//指定了一个目录
            {
                string? LibLoc = Path.GetDirectoryName(tmploc);
                WrdLoc = LibLoc + @"\xkjd6.cizu.dict.yaml";
                DanLoc = LibLoc + @"\xkjd6.danzi.dict.yaml";
                if (TryLoaLib())
                {
                    _ = MessageBox.Show($"已成功载入指定{MsgBoxLoaTip}",
                                        "提示",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                    TBLibLoc.Text = LibLoc;
                }
            }
        }

        private void TBLibLoc_TextChanged(object sender, TextChangedEventArgs e)//每次词库载入成功
        {
            BacOut = false;//需要重新备份
            if (CBDntLog.IsChecked == false && !LogOut && TBLog.Text.Length > 0//如果存在未导出的日志
                && MessageBox.Show("有日志未导出，是否要直接清空？",
                                   "提示",
                                   MessageBoxButton.YesNo,
                                   MessageBoxImage.Question) == MessageBoxResult.No//如果选择了不要直接清空
                && !TryLogCor(SetLogLoc()))//但最后还是导出失败了
            {
                _ = MessageBox.Show("导出失败，日志将直接清空。",
                                    "提示",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
            }
            TBLog.Clear();//清空日志

            //根据备份状态启用或禁用控件
            BacStaSwt();

            //根据日志状态启用或禁用控件
            LogStaSwt();
        }

        private void BacStaSwt()//根据备份状态启用或禁用控件
        {
            if (BacOut)//如果已备份
            {
                ButSetBacLoc.IsEnabled = false;
                TBBacLoc.IsEnabled = false;
                CBDntBac.IsEnabled = false;
                ButBac.IsEnabled = false;
                LBBacSta.Visibility = Visibility.Visible;
                LBBacSta.Content = "√已备份";
                LBBacSta.Foreground = Brushes.Green;
            }
            else//如果还没备份
            {
                CBDntBac.IsEnabled = true;
                LBBacSta.Content = "×未备份";
                LBBacSta.Foreground = Brushes.Red;
                if (CBDntBac.IsChecked == true)//如果不要备份
                {
                    ButSetBacLoc.IsEnabled = false;
                    TBBacLoc.IsEnabled = false;
                    LBBacSta.Visibility = Visibility.Hidden;
                    ButBac.IsEnabled = false;
                }
                else//如果要备份
                {
                    ButSetBacLoc.IsEnabled = true;
                    TBBacLoc.IsEnabled = true;
                    LBBacSta.Visibility = Visibility.Visible;
                    ButBac.IsEnabled = TBBacLoc.Text.Length > 0;//能不能备份
                }
            }
        }

        private void LogStaSwt()//根据日志状态启用或禁用控件
        {
            if (LogOut)//如果已导出
            {
                ButSetLogLoc.IsEnabled = false;
                TBLogLoc.IsEnabled = false;
                CBDntLog.IsEnabled = false;
                ButLog.IsEnabled = false;
                LBLogSta.Visibility = Visibility.Visible;
                LBLogSta.Content = "√已导出";
                LBLogSta.Foreground = Brushes.Green;
            }
            else//如果还没导出
            {
                CBDntLog.IsEnabled = true;
                LBLogSta.Content = "×未导出";
                LBLogSta.Foreground = Brushes.Red;
                if (CBDntLog.IsChecked == true)//如果不要导出
                {
                    ButSetLogLoc.IsEnabled = false;
                    TBLogLoc.IsEnabled = false;
                    LBLogSta.Visibility = Visibility.Hidden;
                    ButLog.IsEnabled = false;
                }
                else//如果要导出
                {
                    ButSetLogLoc.IsEnabled = true;
                    TBLogLoc.IsEnabled = true;
                    LBLogSta.Visibility = Visibility.Visible;
                    ButLog.IsEnabled = TBLogLoc.Text.Length > 0;//能不能导出
                }
            }
        }

        private void CBDntBac_Checked(object sender, RoutedEventArgs e)//禁用备份
        {
            BacStaSwt();
        }

        private void CBDntBac_Unchecked(object sender, RoutedEventArgs e)//启用备份
        {
            BacStaSwt();
        }

        private void CBDntLog_Checked(object sender, RoutedEventArgs e)//禁用日志
        {
            LogStaSwt();
            TabLog.Visibility = Visibility.Collapsed;
        }

        private void CBDntLog_Unchecked(object sender, RoutedEventArgs e)//启用日志
        {
            LogStaSwt();
            TabLog.Visibility = Visibility.Visible;
        }

        private void TBLog_TextChanged(object sender, TextChangedEventArgs e)//日志有变时
        {
            LogOut = false;
            LogStaSwt();
        }

        private static string SetBacLoc()//选择备份位置
        {
            SaveFileDialog SetLoc = new()
            {
                DefaultExt = ".yaml",
                FileName = "xkjd6.cizu.dict(Bac).yaml",
                Filter = "Rime键道词库 (.yaml)|*.yaml",
                Title = "备份到"
            };
            return SetLoc.ShowDialog() == true ? SetLoc.FileName : string.Empty;
        }

        private void ButSetBacLoc_Click(object sender, RoutedEventArgs e)//将选择的备份位置载入文本框
        {
            TBBacLoc.Text = SetBacLoc();
            BacStaSwt();//根据备份状态启用或禁用控件
        }

        private bool TryBacCor(string BacLoc)//备份词库的核心操作
        {
            try
            {
                File.Copy(WrdLoc, BacLoc, true);
                return true;
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"词库备份出错: {ex.Message}",
                                    "错误",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                return false;
            }
        }

        private void ButBac_Click(object sender, RoutedEventArgs e)//执行词库备份
        {
            BacOut = TryBacCor(TBBacLoc.Text);
            BacStaSwt();//根据备份状态启用或禁用控件
        }

        private static string SetLogLoc()//选择日志位置
        {
            SaveFileDialog SetLoc = new()
            {
                DefaultExt = ".txt",
                FileName = $"xkjd6.cizu.log({DateTime.Now:yyyy-MM-dd-HH-mm-ss}).txt",
                Filter = "词库修改日志 (.txt)|*.txt",
                Title = "日志将放在"
            };
            return SetLoc.ShowDialog() == true ? SetLoc.FileName : string.Empty;
        }

        private void ButSetLogLoc_Click(object sender, RoutedEventArgs e)//将选择的日志位置载入文本框
        {
            TBLogLoc.Text = SetLogLoc();
            LogStaSwt();//根据日志状态启用或禁用控件
        }

        private bool TryLogCor(string LogLoc)//导出日志的核心操作
        {
            try
            {
                using StreamWriter StrWriLog = new(LogLoc);
                StrWriLog.Write(TBLog.Text);
                return true;
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"日志导出出错: {ex.Message}",
                                    "错误",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                return false;
            }
        }

        private void ButLog_Click(object sender, RoutedEventArgs e)//执行日志导出
        {
            if (TBLog.Text.Length == 0)
            {
                _ = MessageBox.Show("还未记录到任何日志，无法导出。\r\n修改过程会产生日志，请在所有修改完成后导出日志。",
                                    "提示",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                return;
            }
            LogOut = TryLogCor(TBLog.Text);
            LogStaSwt();//根据日志状态启用或禁用控件
        }

        private void ButHel_Click(object sender, RoutedEventArgs e)//显示帮助
        {
            _ = MessageBox.Show("功能：\r\n"
                                + "加词：在词库中添加一行。\r\n"
                                + "删词：在词库中删除一行。\r\n"
                                + "调频：将长码的词移至短码，短码的词移至剩下最短的空码上。\r\n"
                                + "修改：修改词库中某一行的词或编码。\r\n"
                                + "日志：记录所有的改动，以便查错和回溯。\r\n"
                                + "\r\n注意：\r\n"
                                + "1. 本工具自动编码和检查严格依照官方键道6编码规则。\r\n"
                                + "2. 仅在启动时加载一次词组和单字，请勿同时另行编辑。\r\n"
                                + "3. 词组载入后会按编码重排，可能破坏原有顺序。\r\n"
                                + "4. 每次改动时会覆写词组文件，不会修改其他文件。\r\n"
                                + "5. 调频功能和加词页自动编码仅支持由单字中的字组成的词。\r\n"
                                + "6. 生僻字（双字节字符）可能出错。", "帮助", MessageBoxButton.OK);
        }

        private void ButBas_Click(object sender, RoutedEventArgs e)//获取群号
        {
            Clipboard.SetDataObject("865189947");
            _ = MessageBox.Show("键道官方QQ群号已复制到剪贴板。",
                                "提示",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
        }

        private void ButCod_Click(object sender, RoutedEventArgs e)//源码链接
        {
            Clipboard.SetDataObject("https://github.com/GarthTB/JDLibManager");
            _ = MessageBox.Show("词器v2.0，一个用于维护Rime星空键道6输入法词库的Windows工具。\r\n已开源于Github，源码链接已复制到剪贴板。",
                                "词器",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
        }

        private void TabCon_SelectionChanged(object sender, SelectionChangedEventArgs e)//未备份即修改时提示
        {
            if (TabCon.SelectedIndex == 0)//若没离开，则不提示
                return;
            if (TBLibLoc.Text.Length == 0)
            {
                _ = MessageBox.Show("尚未载入词库，无法修改。请先载入词库。",
                                    "提示",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                TabCon.SelectedIndex = 0;
                return;
            }
            if (CBDntBac.IsChecked == false && !BacOut)
            {
                if (MessageBox.Show("尚未备份。修改将覆写原词库，确定不备份吗？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    CBDntBac.IsChecked = true;
                    return;
                }
                TabCon.SelectedIndex = 0;
            }
        }

        private void WinDowMai_Closing(object sender, System.ComponentModel.CancelEventArgs e)//关闭时有日志未导出，则提示
        {
            if (TBLog.Text.Length > 0 && CBDntLog.IsChecked == false && !LogOut)
            {
                if (MessageBox.Show("有日志未导出，是否要直接退出？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    TabCon.SelectedIndex = 0;
                }
            }
        }

        #endregion

        #region Add页的操作

        private bool ManCoding;//是否正在手动编码
        private byte CodLen = 4;//目标码长，初始为4
        private string AddProWrd = string.Empty;//暂时存储Add页面的输入的词
        private string AddProCod = string.Empty;//暂时存储Add页面的输入的码
        private HashSet<string> AllFulCod = new();//词的所有全码（未排序）
        private readonly DispatcherTimer TmrAddCod = new() { Interval = TimeSpan.FromSeconds(0.4) };

        private void ChkRskAdd()//检查风险
        {
            ButAdd.IsEnabled = false;

            if (TBAddWrd.Text.Length < 2 || CBAddCod.Text.Length == 0)//没词或没码忽略
                return;

            if (HavWrdCod(TBAddWrd.Text, CBAddCod.Text))//有该项，不能加
            {
                LBAddAlr.Visibility = Visibility.Visible;
                return;
            }

            if (CBIgnAdd.IsChecked == false)//不忽略风险，则执行检查
            {
                WarAddCZG.IsChecked = HavWrd(TBAddWrd.Text);//存在该词
                WarAddMWB.IsChecked = HavCod(CBAddCod.Text);//码位被占
                WarAddGDK.IsChecked = !FulShoCod(CBAddCod.Text);//更短空码
                if (!ManCoding) WarAddDMK.IsChecked = CBAddCod.Items.Count > 1;//多码可选
            }

            LBAddAlr.Visibility = Visibility.Hidden;
            ButAdd.IsEnabled = true;
        }

        private void TBAddWrd_TextChanged(object sender, TextChangedEventArgs e)//输入待加的词
        {
            if (TBAddWrd.Text.Length < 2)//防止清空
            {
                LBAddAlr.Visibility = Visibility.Hidden;
                ButAdd.IsEnabled = false;
                return;
            }
            AddProWrd = TBAddWrd.Text;
            if (CutWrd(ref AddProWrd) && AllDan(AddProWrd))//如果能自动编码，则获取自动编码
            {
                WarAddWFZ.IsChecked = false;
                AllFulCod = GetAllFulCod(AddProWrd).ToHashSet();//自动去除重复项
                CBAddCod.ItemsSource = AllFulCod.Select(x => x[..CodLen])//按码长截取
                                                .OrderBy(x => x);//排序
                CBAddCod.SelectedIndex = 0;//自动选中第一个码
            }
            else//如果不符合条件，则显示无法自动
            {
                WarAddWFZ.IsChecked = true;
            }
            ChkRskAdd();
        }

        private void CBAddCod_SelectionChanged(object sender, SelectionChangedEventArgs e)//选择了一个自动编码
        {
            ChkRskAdd();
        }

        private void SliAddCodLen_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)//选择了一个码长
        {
            CodLen = (byte)SliAddCodLen.Value;
            if (AllFulCod.Count == 0)
                return;
            CBAddCod.ItemsSource = AllFulCod.Select(x => x[..CodLen])//按码长截取
                                            .OrderBy(x => x);//排序
            CBAddCod.SelectedIndex = 0;//自动选中第一个
            ChkRskAdd();
        }

        private void ChkManAddCod(object? sender, EventArgs e)//检查手动输入的编码
        {
            if (CBAddCod.Text.Length == 0)//防止清空
            {
                LBAddAlr.Visibility = Visibility.Hidden;
                ButAdd.IsEnabled = false;
            }
            if (CBAddCod.Text.Length > 6)//超长则截短
            {
                CBAddCod.Text = CBAddCod.Text.Remove(6);
            }
            if (TBAddWrd.Text.Length > 1//有词（若能自动编码，则已经有自动的编码）
                && CBAddCod.Text != AddProCod)//码与先前输入的不同
            {
                AddProCod = CBAddCod.Text;
                if (AllFulCod.Count > 0)//能自动编码
                {
                    if (AllFulCod.Any(x => x.StartsWith(AddProCod)))//输入的码与词匹配
                    {
                        ManCoding = false;//相当于选中了一项
                        WarAddMBP.IsChecked = false;
                    }
                    else//输入的码与词不配
                    {
                        ManCoding = true;
                        WarAddMBP.IsChecked = true;
                    }
                }
                else//不能自动编码
                {
                    ManCoding = true;
                    WarAddMBP.IsChecked = false;
                }
                ChkRskAdd();
            }
        }

        private void CBAddCod_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)//手动输入编码开始时
        {
            TmrAddCod.Start();
            TmrAddCod.Tick += ChkManAddCod;//每0.4秒检查一次手动编码
        }

        private void CBAddCod_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)//手动输入编码完成时
        {
            TmrAddCod.Stop();
        }

        private void CBIgnAdd_Checked(object sender, RoutedEventArgs e)//忽略风险
        {
            WarAddCZG.IsChecked = false;//存在该词
            WarAddGDK.IsChecked = false;//更短空码
            WarAddMWB.IsChecked = false;//码位被占
            WarAddDMK.IsChecked = false;//多码可选
        }

        private void CBIgnAdd_Unchecked(object sender, RoutedEventArgs e)//不忽略风险
        {
            ChkRskAdd();
        }

        private void ButAdd_Click(object sender, RoutedEventArgs e)//加词
        {
            //插入新词
            DicWrdAdd(TBAddWrd.Text, CBAddCod.Text);

            //写入词库
            if (!TryWriWrd())//失败，则直接返回
                return;

            //记录日志
            if (CBDntLog.IsChecked == false)//禁用日志，则直接返回
                WriTabLog(1);

            //防止再次加入
            LBAddAlr.Visibility = Visibility.Visible;
            ButAdd.IsEnabled = false;
        }

        #endregion

        #region Del页的操作

        private HashSet<string> AllCodDel = new();//词库里现有的所有码

        private void ChkRskDel()//检查风险
        {
            LBDelAlr.Visibility = Visibility.Hidden;
            if (TBDelWrd.Text.Length == 0 || CBDelCod.Text.Length == 0)//没有目标
            {
                ButDel.IsEnabled = false;
                return;
            }
            if (CBIgnDel.IsChecked == false)//不忽略风险，则执行检查
            {
                WarDelDMK.IsChecked = AllCodDel.Count > 1;//多码可选
                WarDelSHL.IsChecked = HavLonCod(CBDelCod.Text);//删后留空
            }
            ButDel.IsEnabled = true;
        }

        private void ReloadCodDel()//往复选框中填放编码
        {
            if (AllCodDel.Count == 0)
            {
                LBDelAlr.Visibility = Visibility.Visible;
                ButDel.IsEnabled = false;
                return;
            }
            LoadCodToCBB(AllCodDel, ref CBDelCod);
        }

        private void TBDelWrd_TextChanged(object sender, TextChangedEventArgs e)//输入待删的词
        {
            if (TBDelWrd.Text.Length == 0)//清空时
            {
                LBDelAlr.Visibility = Visibility.Hidden;
                ButDel.IsEnabled = false;
                return;
            }
            AllCodDel = TryGetAllCod(TBDelWrd.Text).ToHashSet();
            ReloadCodDel();
            ChkRskDel();
        }

        private void CBDelCod_SelectionChanged(object sender, SelectionChangedEventArgs e)//选择了一个已有的编码
        {
            ChkRskDel();
        }

        private void CBIgnDel_Checked(object sender, RoutedEventArgs e)//忽略风险
        {
            WarDelDMK.IsChecked = false;//多码可选
            WarDelSHL.IsChecked = false;//删后留空
        }

        private void CBIgnDel_Unchecked(object sender, RoutedEventArgs e)//不忽略风险
        {
            ChkRskDel();
        }

        private void ButDel_Click(object sender, RoutedEventArgs e)//删词
        {
            //在词库中删掉选中的词
            DicWrdDel(TBDelWrd.Text, CBDelCod.Text);

            //写入词库
            if (!TryWriWrd())//失败，则直接返回
                return;

            //记录日志
            if (CBDntLog.IsChecked == false)//禁用日志，则直接返回
                WriTabLog(2);

            //补位提示
            if (WarDelSHL.IsChecked == true)
            {
                _ = MessageBox.Show("该码删除后会变成空位，请到修改页面补位。",
                                    "提示",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
            }

            //在现有列表中删掉选中的码
            _ = AllCodDel.Remove(CBDelCod.Text);
            CBDelCod.Text = string.Empty;

            //防止再次删除
            LBDelAlr.Visibility = Visibility.Visible;
            ButDel.IsEnabled = false;
        }

        #endregion

        #region Exc页的操作

        private HashSet<string> AllCodSho = new();//词库里现有的所有短码
        private HashSet<string> AllCodLon = new();//词库里现有的所有长码
        private string LngthnCod = string.Empty;//短码将要加长到的码

        private void ChkRskExc()//检查风险
        {
            LBExcAlr.Visibility = Visibility.Hidden;
            ButExc.IsEnabled = false;
            if (TBExcWrdSho.Text.Length == 0
                || CBExcCodSho.Text.Length == 0
                || TBExcWrdLon.Text.Length == 0
                || CBExcCodLon.Text.Length == 0
                || TBExcWrdSho.Text == TBExcWrdLon.Text
                || CBExcCodSho.Text.Length == CBExcCodLon.Text.Length)//没有目标或没有改动
            {
                return;
            }

            if (!CBExcCodLon.Text.StartsWith(CBExcCodSho.Text))//如果两码无冲突
            {
                WarExcWCT.IsChecked = true;
                return;
            }
            WarExcWCT.IsChecked = false;

            string ProWrd = TBExcWrdSho.Text;
            if (!(CutWrd(ref ProWrd) && AllDan(ProWrd)))//如果不能自动编码，则返回
            {
                WarExcGZB.IsChecked = true;
                return;
            }
            LngthnCod = GetLngthnCod(TBExcWrdSho.Text, CBExcCodSho.Text);
            if (LngthnCod.Length == 0)//如果短码加长方法不唯一，则返回
            {
                WarExcGZB.IsChecked = true;
                return;
            }
            if (WrdCodMch(TBExcWrdSho.Text, CBExcCodLon.Text))//如果短码词恰好可以和长码词互换位置
            {
                LngthnCod = CBExcCodLon.Text;
            }
            WarExcGZB.IsChecked = false;

            if (CBIgnExc.IsChecked == false)//不忽略风险，则执行检查
            {
                WarExcMWB.IsChecked = HavCod(LngthnCod);//码位被占
                WarExcDMK.IsChecked = CBExcCodSho.Items.Count > 1 || CBExcCodLon.Items.Count > 1;//多码可选
                WarExcTHL.IsChecked = HavLonCod(CBExcCodLon.Text);//调后留空
            }

            ButExc.IsEnabled = true;
        }

        private void ReloadCodExcSho()//往复选框中填放短码
        {
            if (AllCodSho.Count == 0)
            {
                LBExcAlr.Visibility = Visibility.Visible;
                ButExc.IsEnabled = false;
                return;
            }
            LoadCodToCBB(AllCodSho, ref CBExcCodSho);
        }

        private void ReloadCodExcLon()//往复选框中填放长码
        {
            if (AllCodLon.Count == 0)
            {
                LBExcAlr.Visibility = Visibility.Visible;
                ButExc.IsEnabled = false;
                return;
            }
            LoadCodToCBB(AllCodLon, ref CBExcCodLon);
        }

        private void TBExcWrdSho_TextChanged(object sender, TextChangedEventArgs e)//输入短码的词
        {
            if (TBExcWrdSho.Text.Length == 0)//清空时
            {
                LBExcAlr.Visibility = Visibility.Hidden;
                ButExc.IsEnabled = false;
                return;
            }
            AllCodSho = TryGetAllCod(TBExcWrdSho.Text).ToHashSet();
            ReloadCodExcSho();
            ChkRskExc();
        }

        private void CBExcCodSho_SelectionChanged(object sender, SelectionChangedEventArgs e)//选择了一个对应的短码
        {
            ChkRskExc();
        }

        private void TBExcWrdLon_TextChanged(object sender, TextChangedEventArgs e)//输入长码的词
        {
            if (TBExcWrdLon.Text.Length == 0)//清空时
            {
                LBExcAlr.Visibility = Visibility.Hidden;
                ButExc.IsEnabled = false;
                return;
            }
            AllCodLon = TryGetAllCod(TBExcWrdLon.Text).ToHashSet();
            ReloadCodExcLon();
            ChkRskExc();
        }

        private void CBExcCodLon_SelectionChanged(object sender, SelectionChangedEventArgs e)//选择了一个对应的长码
        {
            ChkRskExc();
        }

        private void CBIgnExc_Checked(object sender, RoutedEventArgs e)//忽略风险
        {
            WarExcMWB.IsChecked = false;//码位被占
            WarExcDMK.IsChecked = false;//多码可选
            WarExcTHL.IsChecked = false;//调后留空
        }

        private void CBIgnExc_Unchecked(object sender, RoutedEventArgs e)//不忽略风险
        {
            ChkRskExc();
        }

        private void DicWrdExc()//进行交换的核心操作
        {
            //用长码词换掉短码词
            for (int i = 0; i < DicWrd[CBExcCodSho.Text].Count; i++)
            {
                if (DicWrd[CBExcCodSho.Text][i] == TBExcWrdSho.Text)
                {
                    DicWrd[CBExcCodSho.Text][i] = TBExcWrdLon.Text;
                    break;
                }
            }

            //删掉原来的长码词
            DicWrdDel(TBExcWrdLon.Text, CBExcCodLon.Text);

            //把短码词加到加长码上
            DicWrdAdd(TBExcWrdSho.Text, LngthnCod);
        }

        private void ButExc_Click(object sender, RoutedEventArgs e)//调换
        {
            //进行交换的核心操作
            DicWrdExc();

            //写入词库
            if (!TryWriWrd())//失败，则直接返回
                return;

            //记录日志
            if (CBDntLog.IsChecked == false)//禁用日志，则直接返回
                WriTabLog(3);

            //补位提示
            if (WarExcTHL.IsChecked == true)
            {
                _ = MessageBox.Show("调换后原本的长码会变成空位，请到修改页面补位。",
                                    "提示",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
            }

            //清除两个复选框
            CBExcCodSho.Text = string.Empty;
            CBExcCodLon.Text = string.Empty;

            //防止再次调换
            LBExcAlr.Visibility = Visibility.Visible;
            ButExc.IsEnabled = false;
        }

        #endregion

        #region Edi页的操作

        private HashSet<string> AllCodEdi = new();//词库里现有的所有码

        private void SliEdiWrdCod_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)//切换改词和改码
        {
            if (SliEdiWrdCod.Value == 0)//0是改码
            {
                TBEdiWrdAft.Clear();
                TBEdiWrdAft.IsEnabled = false;
                TBEdiCodAft.IsEnabled = true;
                return;
            }
            TBEdiCodAft.Clear();
            TBEdiWrdAft.IsEnabled = true;//1是改词
            TBEdiCodAft.IsEnabled = false;
        }

        private void ChkRskEdi()//检查风险
        {
            if (TBEdiWrdBef.Text.Length == 0 || CBEdiCodBef.Text.Length == 0)//如果没输入原有条目
            {
                return;
            }

            if (SliEdiWrdCod.Value == 0)//如果要改码
            {
                if (TBEdiCodAft.Text.Length == 0 || CBEdiCodBef.Text == TBEdiCodAft.Text)//没有目标或没有改动
                {
                    return;
                }
                if (CBIgnEdi.IsChecked == false)//不忽略风险，则执行检查
                {
                    WarEdiRYC.IsChecked = false;//冗余词与改码无关
                    WarEdiGDK.IsChecked = !FulShoCod(TBEdiCodAft.Text);//更短空码
                    WarEdiMWB.IsChecked = HavCod(TBEdiCodAft.Text);//码位被占
                    WarEdiGHL.IsChecked = HavLonCod(CBEdiCodBef.Text);//改后留空
                    string ProWrd = TBEdiWrdBef.Text;
                    if (CutWrd(ref ProWrd) && AllDan(ProWrd))//能自动编码
                        WarEdiMBP.IsChecked = !WrdCodMch(TBEdiWrdBef.Text, TBEdiCodAft.Text);//码不配词
                }
            }
            else//如果要改词
            {
                if (TBEdiWrdAft.Text.Length == 0 || TBEdiWrdBef.Text == TBEdiWrdAft.Text)//没有目标或没有改动
                {
                    return;
                }
                if (CBIgnEdi.IsChecked == false)//不忽略风险，则执行检查
                {
                    WarEdiGDK.IsChecked = false;//更短空码与改词无关
                    WarEdiMWB.IsChecked = false;//码位被占与改词无关
                    WarEdiGHL.IsChecked = false;//改后留空与改词无关
                    WarEdiRYC.IsChecked = HavWrd(TBEdiWrdAft.Text);//冗余词
                    string ProWrd = TBEdiWrdAft.Text;
                    if (CutWrd(ref ProWrd) && AllDan(ProWrd))//能自动编码
                        WarEdiMBP.IsChecked = !WrdCodMch(TBEdiWrdAft.Text, CBEdiCodBef.Text);//码不配词
                }
            }

            ButEdi.IsEnabled = true;
        }

        private void ReloadCodEdi()//往复选框中填放编码
        {
            if (AllCodEdi.Count == 0)
            {
                LBEdiAlr.Visibility = Visibility.Visible;
                ButEdi.IsEnabled = false;
                return;
            }
            LoadCodToCBB(AllCodEdi, ref CBEdiCodBef);
        }

        private void TBEdiWrdBef_TextChanged(object sender, TextChangedEventArgs e)//输入原有的词
        {
            if (TBEdiWrdBef.Text.Length == 0)//清空时
            {
                LBEdiAlr.Visibility = Visibility.Hidden;
                ButEdi.IsEnabled = false;
                return;
            }
            AllCodEdi = TryGetAllCod(TBEdiWrdBef.Text).ToHashSet();
            ReloadCodEdi();
            ChkRskEdi();
        }

        private void CBEdiCodBef_SelectionChanged(object sender, SelectionChangedEventArgs e)//选择了一个要被修改的码
        {
            ChkRskEdi();
        }

        private void TBEdiWrdAft_TextChanged(object sender, TextChangedEventArgs e)//输入要改成的词
        {
            if (TBEdiWrdAft.Text.Length == 0)//清空时
            {
                LBEdiAlr.Visibility = Visibility.Hidden;
                ButEdi.IsEnabled = false;
                return;
            }
            ChkRskEdi();
        }

        private void TBEdiCodAft_TextChanged(object sender, TextChangedEventArgs e)//输入要改成的码
        {
            if (TBEdiCodAft.Text.Length == 0)//清空时
            {
                LBEdiAlr.Visibility = Visibility.Hidden;
                ButEdi.IsEnabled = false;
                return;
            }
            ChkRskEdi();
        }

        private void CBIgnEdi_Checked(object sender, RoutedEventArgs e)//忽略风险
        {
            WarEdiGDK.IsChecked = false;//更短空码
            WarEdiMWB.IsChecked = false;//码位被占
            WarEdiRYC.IsChecked = false;//冗余词
            WarEdiGHL.IsChecked = false;//改后留空
            WarEdiMBP.IsChecked = false;//码不配词
        }

        private void CBIgnEdi_Unchecked(object sender, RoutedEventArgs e)//不忽略风险
        {
            ChkRskEdi();
        }

        private void DicWrdEdiCod()//进行改码的核心操作
        {
            //删掉原来的条目
            DicWrdDel(TBEdiWrdBef.Text, CBEdiCodBef.Text);

            //插入全新的条目
            DicWrdAdd(TBEdiWrdBef.Text, TBEdiCodAft.Text);
        }

        private void DicWrdEdiWrd()//进行改词的核心操作
        {
            //用后来的词换掉原来的词
            for (int i = 0; i < DicWrd[CBEdiCodBef.Text].Count; i++)
            {
                if (DicWrd[CBEdiCodBef.Text][i] == TBEdiWrdBef.Text)
                {
                    DicWrd[CBEdiCodBef.Text][i] = TBEdiWrdAft.Text;
                    return;
                }
            }
        }

        private void ButEdi_Click(object sender, RoutedEventArgs e)//修改
        {
            //进行改词或改码
            if (SliEdiWrdCod.Value == 0)
            {
                DicWrdEdiCod();
            }
            else
            {
                DicWrdEdiWrd();
            }

            //写入词库
            if (!TryWriWrd())//失败，则直接返回
                return;

            //记录日志
            if (CBDntLog.IsChecked == false)//禁用日志，则直接返回
                WriTabLog(4);

            //补位提示
            if (WarEdiGHL.IsChecked == true)
            {
                _ = MessageBox.Show("修改后原有的码会变成空位，请到修改页面补位。",
                                    "提示",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
            }

            //清除复选框
            CBEdiCodBef.Text = string.Empty;

            //防止再次调换
            LBEdiAlr.Visibility = Visibility.Visible;
            ButEdi.IsEnabled = false;
        }

        #endregion
    }
}