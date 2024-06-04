using System.Windows;
using System.Windows.Controls;

namespace JDLibManager
{
    public partial class MainWindow : Window//Del页的操作
    {
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
    }
}
