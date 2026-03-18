using System;
using System.Windows.Media;

namespace PowerShot.Models
{
    // ============================================================
    // Data Model for Explorer ListView items
    // ============================================================
    public class ExplorerItem
    {
        public ImageSource Icon { get; set; }
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime LastModified { get; set; }
    }
}
