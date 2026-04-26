using System;
using System.Windows.Media;
using PowerShot.Utils;

namespace PowerShot.Models
{
    public class ExplorerItem
    {
        private ImageSource _icon;
        public ImageSource Icon
        {
            get
            {
                if (_icon == null) _icon = IconHelper.GetIcon(FullPath, IsDirectory);
                return _icon;
            }
        }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime LastModified { get; set; }
    }
}
