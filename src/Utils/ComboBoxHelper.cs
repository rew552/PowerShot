using System.Windows.Controls;

namespace PowerShot
{
    internal static class ComboBoxHelper
    {
        public static void SetSelectedByTag(ComboBox cb, string tagValue)
        {
            if (cb == null || string.IsNullOrEmpty(tagValue)) return;
            foreach (ComboBoxItem item in cb.Items)
            {
                if ((string)item.Tag == tagValue)
                {
                    cb.SelectedItem = item;
                    return;
                }
            }
        }

        public static string GetSelectedTag(ComboBox cb)
        {
            if (cb == null) return null;
            ComboBoxItem item = cb.SelectedItem as ComboBoxItem;
            return item != null ? item.Tag as string : null;
        }
    }
}
