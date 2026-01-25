using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Serilog;
using White_Desert.Models;
using White_Desert.Models.GhostBridge;
using White_Desert.Services.Contracts;

namespace White_Desert.Helper;

public static class TreeDataGridHelper
{
    public static AvaloniaList<BdoNode> BuildSearchTree(IPazService paz, string query)
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

        var rootList = new AvaloniaList<BdoNode>();

        var folderCache = new Dictionary<string, BdoNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in searchResults)
        {
            var cleanPath = result.FolderPath.Replace('\\', '/');
            var parts = cleanPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

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

            var fileNode = new BdoNode(result.FileName, cleanPath + result.FileName, false, result.Index);

            if (currentParent == null) rootList.Add(fileNode);
            else currentParent.Children.Add(fileNode);
        }

        SortNodesRecursive(rootList);

        return rootList;
    }

    public static AvaloniaList<BdoNode> BuildFolderTree(List<FolderNameTuple> allFolder, BdoNode placeholder)
    {
        var preparedPaths = allFolder
            .AsParallel()
            .Select(f =>
            {
                var original = f.FolderName.ToString();
                return (
                    Clean: original.Replace('\\', '/').Trim('/'),
                    Original: original,
                    Index: f.FolderIndex
                );
            })
            .Where(x => !string.IsNullOrEmpty(x.Clean))
            .OrderBy(x => x.Clean.Length)
            .ToList();

        var nodeCache = new Dictionary<string, BdoNode>(allFolder.Count, StringComparer.OrdinalIgnoreCase);
        var roots = new AvaloniaList<BdoNode>();

        foreach (var (cleanPath, originalPath, folderIndex) in preparedPaths)
        {
            var parts = cleanPath.Split('/');
            string currentPathAcc = "";
            BdoNode parentNode = null;

            for (int i = 0; i < parts.Length; i++)
            {
                var partName = parts[i];
                currentPathAcc = i == 0 ? partName : $"{currentPathAcc}/{partName}";

                if (!nodeCache.TryGetValue(currentPathAcc, out BdoNode currentNode))
                {
                    int? idx = (currentPathAcc == cleanPath) ? (int)folderIndex : null;
                    string fullPathForPaz = currentPathAcc + "/";

                    currentNode = new BdoNode(partName, fullPathForPaz, true, idx);

                    currentNode.Children.Add(placeholder);

                    if (parentNode == null) roots.Add(currentNode);
                    else parentNode.Children.Add(currentNode);

                    nodeCache[currentPathAcc] = currentNode;
                }

                if (parentNode != null && parentNode.Children.Contains(placeholder))
                {
                    parentNode.Children.Remove(placeholder);
                }

                parentNode = currentNode;
            }
        }

        SortNodesRecursive(roots);

        return roots;
    }

    private static void SortNodesRecursive(IList<BdoNode> nodes)
    {
        if (nodes.Count == 0) return;

        var sorted = nodes
            .OrderByDescending(x => x.IsFolder)
            .ThenBy(x => x.Name)
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            nodes[i] = sorted[i];

            if (sorted[i].Children != null && sorted[i].Children.Count > 0)
            {
                SortNodesRecursive(sorted[i].Children);
            }
        }
    }

    public static List<BdoNode> GetFolderChildren(IPazService paz, BdoNode parent)
    {
        var result = new List<BdoNode>();

        if (parent.EntryIndex.HasValue && parent.EntryIndex.Value != -1)
        {
            try
            {
                var fileIndices = paz.GetFilesInFolder((uint)parent.EntryIndex.Value);
                if (fileIndices != null && fileIndices.Length > 0)
                {
                    var fileNames = paz.GetFileNames(fileIndices);
                    for (int i = 0; i < fileIndices.Length; i++)
                    {
                        var separator = parent.FullPath.EndsWith("/") ? "" : "/";
                        var fullPath = parent.FullPath + separator + fileNames[i];

                        result.Add(new BdoNode(fileNames[i], fullPath, false, (int)fileIndices[i]));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting files for folder {Path}", parent.FullPath);
            }
        }

        return result.OrderBy(n => n.Name).ToList();
    }

    public static List<uint> CollectIndicesParallel(IPazService paz, IEnumerable<BdoNode> selectedNodes)
    {
        var results = new ConcurrentBag<uint>();

        Parallel.ForEach(selectedNodes, node =>
        {
            var localList = new List<uint>();

            if (node.IsFolder)
            {
                CollectIndicesRecursive(paz, node, localList);
            }
            else if (node.EntryIndex.HasValue)
            {
                localList.Add((uint)node.EntryIndex.Value);
            }

            foreach (var id in localList) results.Add(id);
        });

        return results.Distinct().ToList();
    }

    private static void CollectIndicesRecursive(IPazService paz, BdoNode parent, List<uint> results)
    {
        if (parent.EntryIndex is { } folderIdx && folderIdx != -1)
        {
            try
            {
                var fileIndices = paz.GetFilesInFolder((uint)folderIdx);
                foreach (var idx in fileIndices)
                {
                    results.Add((uint)idx);
                }
            }
            catch
            {
                // Ignoriere Fehler bei einzelnen Ordnern
            }
        }
        
        if (parent.Children != null)
        {
            foreach (var child in parent.Children)
            {
                if (child.IsFolder)
                {
                    CollectIndicesRecursive(paz, child, results);
                }
            }
        }
    }


    private static AvaloniaList<BdoNode> SortNodes(AvaloniaList<BdoNode> nodes)
    {
        var sortedList = nodes.OrderByDescending(x => x.IsFolder).ThenBy(x => x.Name).ToList();
        return new AvaloniaList<BdoNode>(sortedList);
    }
}