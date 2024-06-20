using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace JDLibManager
{
    public partial class MainWindow : Window//全局变量和方法
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Lib指Rime的用户文件夹，或词库所在的文件夹，Wrd指词组，Dan指单字
        /// Set, Add, Del, Exc, Edi, Log分别指六个功能页面
        /// </summary>

        private readonly HashSet<char> YinEle = new("bcdefghjklmnpqrstwxyz");//键道的音码码元
        private readonly HashSet<char> XngEle = new("aiouv");//键道的形码码元
        private readonly HashSet<char> AllEle = new("abcdefghijklmnopqrstuvwxyz");//键道的所有码元
        private const string ChnPat = @"[^\u4e00-\u9fff]";//用于正则匹配的中文模式
        private string WrdLoc = string.Empty;//词组文件位置
        private string DanLoc = string.Empty;//单字文件位置
        private readonly List<string> LstHed = new(5);//词组的文件头
        private readonly SortedDictionary<string, List<string>> DicWrd = new();//词组，键是码
        private readonly Dictionary<string, HashSet<string>> DicDan = new();//单字，键是字，char装不下

        private void DicWrdAdd(string word, string code)//往词组里加东西
        {
            if (DicWrd.ContainsKey(code)) DicWrd[code].Add(word);//如果有这一码，则在这码上加一词
            else DicWrd.Add(code, new List<string> { word });//如果没这一码，则新建
        }

        private void DicWrdDel(string word, string code)//在词组里删东西
        {
            if (DicWrd[code].Count == 1) DicWrd.Remove(code);//如果这码只有这一个词，则删掉整个条目
            else DicWrd[code].Remove(word);//否则只删这一个词
        }

        private void DicDanAdd(string zi, string code)//往单字里加东西
        {
            if (DicDan.ContainsKey(zi)) DicDan[zi].Add(code);//如果有这一字，则在这字上加一码
            else DicDan.Add(zi, new HashSet<string> { code });//如果没这一字，则新建
        }

        private static void CBBLoad(HashSet<string> codes, ref ComboBox CBB)//往某个复选框中填放一组编码
        {
            CBB.ItemsSource = codes.Count == 1 ? codes : codes.OrderBy(x => x);
            CBB.SelectedIndex = 0;//自动选中第一个
        }

        private static bool IsValid(string wordIn, out string wordOut)//除去指定字符串中不参与编码的字符，返回可用性
        {
            wordOut = Regex.Replace(wordIn, ChnPat, string.Empty);//除去非中文字符
            if (wordOut.Length < 2)
                return false;
            if (wordOut.Length > 4)
                wordOut = wordOut[..3] + wordOut[^1];//只取前3字和末字
            return true;
        }

        private bool AllDan(string word)//检查指定字符串中的字是否全在单字中
        {
            return !word.Any(c => !DicDan.ContainsKey(c.ToString()));
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
                if (!HavCod(LngthnCod[0][..i]))
                    return LngthnCod[0][..i];

            return LngthnCod[0];
        }

        private bool WrdCodMch(string ProWrd, string ProCod)//检查词和码是否匹配
        {
            return GetAllFulCod(ProWrd).Any(x => x.StartsWith(ProCod));//若存在以该码开头的全码，则返回真
        }

        private bool HavWrdCod(string ProWrd, string ProCod)//检查词库中是否已有该项
        {
            return HavCod(ProCod) && DicWrd[ProCod].Contains(ProWrd);//若有码且码上有这一词，则返回真
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
                    foreach (string wrd in wrds)
                        StrWriWrd.WriteLine(wrd + '\t' + cod);//写入词库
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("写入词库出错: " + ex.Message,
                                "错误",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                return false;
            }
        }

        private string GetRskInf(byte Pag)//获取指定页的风险提示代号
        {
            Dictionary<byte, List<CheckBox>> AllWarCBs = new()//每页的编号和对应的风险提示
            {
                { 1, new List<CheckBox> { WarAddWFZ, WarAddCZG, WarAddGDK, WarAddMWB, WarAddDMK, WarAddMBP } },
                { 2, new List<CheckBox> { WarDelDMK, WarDelSHL } },
                { 3, new List<CheckBox> { WarExcMWB, WarExcDMK, WarExcTHL } },
                { 4, new List<CheckBox> { WarEdiGDK, WarEdiMWB, WarEdiRYC, WarEdiGHL, WarEdiMBP } }
            };
            string RskInf = string.Empty;
            if (AllWarCBs.TryGetValue(Pag, out List<CheckBox>? WarCBs))//某页上的风险提示
                foreach (CheckBox CB in WarCBs)//有该风险，则为1；无该风险，则为0
                    RskInf = CB.IsChecked == true ? RskInf + "1" : RskInf + "0";
            return RskInf;
        }

        private void WriTabLog(byte Pag)//把指定页面的日志写到日志页
        {
            switch (Pag)
            {
                case 1:
                    if (CBIgnAdd.IsChecked == true)//记录不带风险信息的日志
                        TBLog.Text += $"{AddingWrd}\t{AddingCod}\t{Pag}\t忽略风险\r\n";
                    else//记录带风险信息的日志
                        TBLog.Text += $"{AddingWrd}\t{AddingCod}\t{Pag}\t{GetRskInf(Pag)}\r\n";
                    break;
                case 2:
                    if (CBIgnDel.IsChecked == true)//记录不带风险信息的日志
                        TBLog.Text += $"{TBDelWrd.Text}\t{CBDelCod.Text}\t{Pag}\t忽略风险\r\n";
                    else//记录带风险信息的日志
                        TBLog.Text += $"{TBDelWrd.Text}\t{CBDelCod.Text}\t{Pag}\t{GetRskInf(Pag)}\r\n";
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
    }
}
