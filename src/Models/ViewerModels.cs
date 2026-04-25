using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PowerShot
{
    [DataContract]
    public class FolderNode
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string Path { get; set; }
        [DataMember] public List<FolderNode> Children { get; set; }
        [DataMember] public List<FileItem> Files { get; set; }

        public FolderNode()
        {
            Children = new List<FolderNode>();
            Files = new List<FileItem>();
        }
    }

    [DataContract]
    public class FileItem
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public string Path { get; set; }
        [DataMember] public string Date { get; set; }
        [DataMember] public long Size { get; set; }
    }
}

