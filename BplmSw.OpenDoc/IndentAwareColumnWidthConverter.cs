using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace BplmSw.OpenDoc
{
    public class IndentAwareColumnWidthConverter: IValueConverter
    {
        public double Indent { get; set; } = 19.0; // 默认缩进

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var item = value as TreeViewItem;
            if (item == null)
                return 0;

            double baseWidth = 0;
            if (parameter != null && double.TryParse(parameter.ToString(), out baseWidth))
            {
            }
            else
            {
                baseWidth = 200; 
            }

            int depth = GetDepth(item);
            double width = baseWidth - (depth * Indent);
            // 每一级 Grid 前面有一个 toggle button 在占用空间。每层缩进 19px。
            
            return width < 0 ? 0 : width;// 确保不小于 0
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private int GetDepth(DependencyObject item)
        {
            int depth = -1; 

            DependencyObject current = item;
            while (current != null)
            {
                if (current is TreeViewItem)
                {
                    depth++;
                }
                else if (current is TreeView)
                {
                    break;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            // 根节点标题内容也会被 19px（切换按钮的宽度）偏移。需要 depth + 1。
            return depth + 1;
        }
    }
}