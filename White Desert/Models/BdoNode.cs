using System.Collections.ObjectModel;
using System.Linq;

namespace White_Desert.Models;

public class BdoNode
{
    public string Name { get; }
    public string FullPath { get; }
    public bool IsFolder { get; }
    public int? EntryIndex { get; set; }
    public ObservableCollection<BdoNode> Children { get; }
    public bool WasLoaded { get; set; } 

    public BdoNode(string name, string fullPath, bool isFolder, int? index = null)
    {
        Name = name;
        FullPath = fullPath;
        IsFolder = isFolder;
        EntryIndex = index;
        
        if (isFolder)
        {
            Children = new ObservableCollection<BdoNode>();
        }
    }
}