using System.Windows;
using System.Windows.Controls;

namespace JDLibManager
{
    public partial class MainWindow : Window//Exc页的操作
    {
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

            if (!(IsValid(TBExcWrdSho.Text, out string valchars) && AllDan(valchars)))//如果不能自动编码，则返回
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
            CBBLoad(AllCodSho, ref CBExcCodSho);
        }

        private void ReloadCodExcLon()//往复选框中填放长码
        {
            if (AllCodLon.Count == 0)
            {
                LBExcAlr.Visibility = Visibility.Visible;
                ButExc.IsEnabled = false;
                return;
            }
            CBBLoad(AllCodLon, ref CBExcCodLon);
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
                MessageBox.Show("调换后原本的长码会变成空位，请到修改页面补位。",
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
    }
}
