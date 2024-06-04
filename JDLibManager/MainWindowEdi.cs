using System.Windows;
using System.Windows.Controls;

namespace JDLibManager
{
    public partial class MainWindow : Window//Edi页的操作
    {
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
    }
}
