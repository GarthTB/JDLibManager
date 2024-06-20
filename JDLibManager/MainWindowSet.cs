using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace JDLibManager
{
    public partial class MainWindow : Window//Set页的操作
    {
        private bool BacOut;//是否已备份成功，初始为否
        private bool LogOut;//是否已导出日志，初始为否
        private const string MsgBoxLoaTip = "目录中的Rime键道词库。\r\n词库只载入一次，如有需要，请重新选择词库。\r\n所有修改都将覆写原词库，且词频和注释会丢失。\r\n建议先备份再修改。";//提示性信息

        private static bool RemoveCom(ref string line)//除去某行上的注释
        {
            if (line.StartsWith('#'))//整行都是注释
            {
                return false;
            }
            if (line.Contains('#'))//行中有注释
            {
                line = line[..line.IndexOf('#')];
            }
            return true;
        }

        private static bool LineValid(string[] SplSli)//判断某行是否为条目
        {
            return SplSli.Length == 2 || (SplSli.Length == 3 && int.TryParse(SplSli[2], out int _));
        }

        private void ReaLibErr()//读取词库出错
        {
            DicWrd.Clear();
            DicDan.Clear();
            LstHed.Clear();
            MessageBox.Show("词库加载出错！请检查词库中有无异常的行，然后重新选择。",
                                "错误",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
        }

        private bool WordValid()//判断词组里是否每个编码的每个字符都有效
        {
            return !DicWrd.Keys.Any(onecod
                => onecod.Any(oneele
                => !AllEle.Contains(oneele)));
        }

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
                    if (!RemoveCom(ref line))//除去注释
                        continue;
                    SplSli = line.Split('\t');//把这行分割
                    if (LineValid(SplSli))//该行是条目，则载入并打断循环
                    {
                        DicWrdAdd(SplSli[0], SplSli[1]);
                        break;
                    }
                    LstHed.Add(line);//不是条目，则载入文件头
                }
                while ((line = StrReaWrd.ReadLine()) != null)//读一行
                {
                    if (!RemoveCom(ref line))//除去注释
                        continue;
                    SplSli = line.Split('\t');//把这行分割
                    if (!LineValid(SplSli))//格式不正确，则立即中止
                    {
                        ReaLibErr();
                        return false;
                    }
                    DicWrdAdd(SplSli[0], SplSli[1]);//载入
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取词组出错: {ex.Message}",
                                    "错误",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                return false;
            }

            //读取完成，读空或含有无效码报错
            if (DicWrd.Count > 0 && WordValid())
                return true;
            ReaLibErr();
            return false;
        }

        private bool DanzValid()//判断单字里是否每个字的每个编码都符合条件
        {
            return !DicDan.Values.Any(codcol
                => codcol.Any(onecod
                => !YinEle.Contains(onecod[0])
                   || !YinEle.Contains(onecod[1])
                   || !XngEle.Contains(onecod[2])));
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
                    if (!RemoveCom(ref line))//除去注释
                        continue;
                    SplSli = line.Split('\t');//把这行分割
                    if (LineValid(SplSli) && SplSli[1].Length > 2)//该行是条目，且大于2码，则载入并打断循环
                    {
                        DicDanAdd(SplSli[0], SplSli[1]);
                        break;
                    }
                }
                while ((line = StrReaDan.ReadLine()) != null)//读一行
                {
                    if (!RemoveCom(ref line))//除去注释
                        continue;
                    SplSli = line.Split('\t');//把这行分割
                    if (!LineValid(SplSli))//格式不正确，则立即中止
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
                MessageBox.Show($"读取单字出错: {ex.Message}",
                                    "错误",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                return false;
            }

            //读取完成，读空或含有无效码报错
            if (DicDan.Count > 0 && DanzValid())
                return true;
            ReaLibErr();
            return false;
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
                MessageBox.Show($"已自动载入程序上级{MsgBoxLoaTip}",
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
                MessageBox.Show($"已自动载入Rime默认{MsgBoxLoaTip}",
                                    "提示",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                TBLibLoc.Text = Path.GetDirectoryName(WrdLoc);
                return;
            }

            //都没有
            MessageBox.Show("未能自动载入Rime键道词库。\r\n建议将此程序放在词库的下级目录中，或手动选择词库位置。",
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
                    MessageBox.Show($"已成功载入指定{MsgBoxLoaTip}",
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
                MessageBox.Show("导出失败，日志将直接清空。",
                                    "提示",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
            }
            //清空日志
            TBLog.Clear();

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
                MessageBox.Show($"词库备份出错: {ex.Message}",
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
                MessageBox.Show($"日志导出出错: {ex.Message}",
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
                MessageBox.Show("还未记录到任何日志，无法导出。\r\n修改过程会产生日志，请在所有修改完成后导出日志。",
                                    "提示",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                return;
            }
            LogOut = TryLogCor(TBLogLoc.Text);
            LogStaSwt();//根据日志状态启用或禁用控件
        }

        private void ButHel_Click(object sender, RoutedEventArgs e)//显示帮助
        {
            MessageBox.Show("功能：\r\n"
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
            MessageBox.Show("键道官方QQ群号已复制到剪贴板。",
                                "提示",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
        }

        private void ButCod_Click(object sender, RoutedEventArgs e)//源码链接
        {
            Clipboard.SetDataObject("https://github.com/GarthTB/JDLibManager");
            MessageBox.Show("词器v2.0，一个用于维护Rime星空键道6输入法词库的Windows工具。\r\n已开源于Github，源码链接已复制到剪贴板。",
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
                MessageBox.Show("尚未载入词库，无法修改。请先载入词库。",
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
    }
}
