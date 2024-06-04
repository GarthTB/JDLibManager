using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace JDLibManager
{
    public partial class MainWindow : Window//Add页的操作
    {
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
    }
}
