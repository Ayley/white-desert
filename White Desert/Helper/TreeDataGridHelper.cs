using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using White_Desert.Models;
using White_Desert.Models.GhostBridge;
using White_Desert.Services.Contracts;

namespace White_Desert.Helper;

public static class TreeDataGridHelper
{
    public static ObservableCollection<BdoNode> BuildSearchTree(IPazService paz, string query)
    {
        var foundIndices = paz.SearchFiles(query);

        var searchResults = foundIndices.AsParallel()
            .Select(index =>
            {
                var entry = paz[index];
                return new
                {
                    Index = index,
                    FolderPath = paz.GetFolderPath(entry.FolderId),
                    FileName = paz.GetFileName(entry.FileId)
                };
            }).ToList();

        var rootList = new ObservableCollection<BdoNode>();
        var folderCache = new Dictionary<string, BdoNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in searchResults)
        {
            var parts = result.FolderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            BdoNode? currentParent = null;
            var pathAcc = "";

            for (var i = 0; i < parts.Length; i++)
            {
                var partName = parts[i];
                pathAcc = i == 0 ? partName : $"{pathAcc}/{partName}";

                if (!folderCache.TryGetValue(pathAcc, out var folderNode))
                {
                    folderNode = new BdoNode(partName, pathAcc + "/", true, -1) { WasLoaded = true };
                    if (currentParent == null) rootList.Add(folderNode);
                    else currentParent.Children.Add(folderNode);
                    folderCache.Add(pathAcc, folderNode);
                }

                currentParent = folderNode;
            }

            var fileNode = new BdoNode(result.FileName, result.FolderPath + result.FileName, false, result.Index);
            if (currentParent == null) rootList.Add(fileNode);
            else currentParent.Children.Add(fileNode);
        }

        return SortNodes(rootList);
    }

    public static List<BdoNode> CreateRootNodes(List<FolderNameTuple> allFolders, BdoNode placeholder)
    {
        return allFolders
            .AsParallel()
            .Select(f => f.FolderName.ToString().Split('/')[0])
            .Distinct()
            .OrderBy(n => n)
            .Select(name =>
            {
                var match = allFolders.First(f => f.FolderName.ToString().StartsWith(name));
                var folderIdx = (match.FolderName.Data != IntPtr.Zero) ? (int)match.FolderIndex : -1;
                var newNode = new BdoNode(name, name + "/", true, folderIdx);
                newNode.Children.Add(placeholder);
                return newNode;
            }).ToList();
    }

    public static List<BdoNode> GetFolderChildren(IPazService paz, ILookup<string, FolderNameTuple> lookup,
        BdoNode parent, BdoNode placeholder)
    {
        var result = new List<BdoNode>();

        if (lookup.Contains(parent.FullPath))
        {
            var subFolders = lookup[parent.FullPath]
                .Select(f =>
                {
                    var fullPath = f.FolderName.ToString();
                    var name = fullPath.TrimEnd('/').Split('/').Last();
                    var node = new BdoNode(name, fullPath, true, (int)f.FolderIndex);
                    node.Children.Add(placeholder);
                    return node;
                });
            result.AddRange(subFolders);
        }

        if (parent.EntryIndex.HasValue && parent.EntryIndex.Value != -1)
        {
            var fileIndices = paz.GetFilesInFolder((uint)parent.EntryIndex.Value);
            var fileNames = paz.GetFileNames(fileIndices);

            var files = fileIndices.Select((idx, i) =>
                new BdoNode(fileNames[i], parent.FullPath + fileNames[i], false, idx));

            result.AddRange(files);
        }

        return result.OrderByDescending(n => n.IsFolder).ThenBy(n => n.Name).ToList();
    }

    public static List<uint> CollectIndicesParallel(IPazService paz, IEnumerable<BdoNode> selectedNodes,
        ILookup<string, FolderNameTuple> lookup)
    {
        var results = new ConcurrentBag<uint>();

        Parallel.ForEach(selectedNodes, node =>
        {
            var localList = new List<uint>();

            if (node.IsFolder)
            {
                CollectIndicesRecursive(paz, lookup, node, localList);
            }
            else if (node.EntryIndex.HasValue)
            {
                localList.Add((uint)node.EntryIndex.Value);
            }

            foreach (var id in localList) results.Add(id);
        });

        return results.Distinct().ToList();
    }

    private static void CollectIndicesRecursive(IPazService paz, ILookup<string, FolderNameTuple> lookup,
        BdoNode parent, List<uint> results)
    {
        if (parent.EntryIndex is { } folderIdx && folderIdx != -1)
        {
            var fileIndices = paz.GetFilesInFolder((uint)folderIdx);
            foreach (var idx in fileIndices)
            {
                results.Add((uint)idx);
            }
        }

        var lookupKey = parent.FullPath;

        if (lookup.Contains(lookupKey))
        {
            foreach (var sub in lookup[lookupKey])
            {
                var subPath = sub.FolderName.ToString();
                var subFolderIdx = (int)sub.FolderIndex;

                CollectIndicesRecursiveInternal(paz, lookup, subPath, subFolderIdx, results);
            }
        }
    }

    private static void CollectIndicesRecursiveInternal(IPazService paz, ILookup<string, FolderNameTuple> lookup,
        string fullPath, int folderIdx, List<uint> results)
    {
        var fileIndices = paz.GetFilesInFolder((uint)folderIdx);
        foreach (var idx in fileIndices) results.Add((uint)idx);

        if (lookup.Contains(fullPath))
        {
            foreach (var sub in lookup[fullPath])
            {
                CollectIndicesRecursiveInternal(paz, lookup, sub.FolderName.ToString(), (int)sub.FolderIndex, results);
            }
        }
    }

    private static ObservableCollection<BdoNode> SortNodes(ObservableCollection<BdoNode> nodes)
    {
        var sortedList = nodes.OrderByDescending(x => x.IsFolder).ThenBy(x => x.Name).ToList();
        return new ObservableCollection<BdoNode>(sortedList);
    }
}