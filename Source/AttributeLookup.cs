using System.Xml;

namespace AICore;

using System.Collections.Generic;
using System.Diagnostics;

// var xml = @"<root><5GSID _name='5GSID'><child _name='child'/></5GSID><GUTI _name='GUTI'/></root>";
// var xmlDoc = new XmlDocument();
// xmlDoc.LoadXml(xml);

// var attributeLookup = new AttributeLookup();
// var result = attributeLookup.FindValue(xmlDoc.DocumentElement, "GUTI");

// if (result != null)
// {
//     Console.WriteLine($"Found: {result.OuterXml}");
// }
// else
// {
//     Console.WriteLine("Not Found");
// }


public class AttributeLookup
{
    private readonly LinkedList<(string Key, List<int> Path)> _knownPaths;
    private readonly Dictionary<string, LinkedListNode<(string Key, List<int> Path)>> _pathNodes;

    public AttributeLookup()
    {
        _knownPaths = new LinkedList<(string Key, List<int> Path)>();
        _pathNodes = new Dictionary<string, LinkedListNode<(string Key, List<int> Path)>>();

        // Initialize with known paths
        // AddKnownPath("5GSID", new List<int> { 3, 3 });
        // AddKnownPath("5GSID", new List<int> { 2, 2 });
        // AddKnownPath("5GSID", new List<int> { 3 });
        // AddKnownPath("GUTI", new List<int> { 2 });
    }

    private void AddKnownPath(string key, List<int> path)
    {
        var node = new LinkedListNode<(string Key, List<int> Path)>((key, path));
        _knownPaths.AddLast(node);
        _pathNodes[GetPathKey(key, path)] = node;
    }

    public XmlNode? FindValue(XmlNode msg, string key)
    {
        // First try to find the value using known paths
        var value = TryKnownPaths(msg, key);
        if (value != null)
        {
            return value;
        }
        // If that didn't work, fall back to the recursive lookup function
        value = AttributeLookupRecursive(msg, key);
        return value;
    }

    private XmlNode? TryKnownPaths(XmlNode msg, string key)
    {
#if DEBUG
        LogTool.Debug($"Trying known paths for {key}");
#endif

        foreach (var (k, path) in _knownPaths)
        {
            if (k != key)
                continue; // Skip paths that don't correspond to the key

            try
            {
#if DEBUG
                LogTool.Debug($"Trying path {string.Join(", ", path)}");
#endif
                var value = PathSearch(msg, path);
                if (value == null)
                    continue;
                if (Validate(value, k))
                {
                    // Move this path to the front
                    MovePathToFront(k, path);
                    return value; // Return value if we successfully followed path
                }
            }
            catch (Exception)
            {
                // If we encounter an error following the path, just move on
            }
        }
        return null; // Return null if no valid value was found
    }

    private XmlNode? PathSearch(XmlNode msg, List<int> path)
    {
        foreach (var step in path)
        {
            if (msg.ChildNodes.Count > step)
            {
                msg = msg.ChildNodes[step];
            }
            else
            {
                return null;
            }
        }
        return msg;
    }

    private bool Validate(XmlNode msg, string key)
    {
        if (msg == null)
            return false;

        var nameAttr = msg.Attributes?["_name"];
        if (nameAttr == null)
            return false;

        var name = nameAttr.Value;
        if (name == "T" || name == "L" || name == "V")
            return false;
        return name == key;
    }

    private XmlNode? AttributeLookupRecursive(XmlNode msg, string key)
    {
        var stopwatch = Stopwatch.StartNew();
        var path = new List<int>();
#if DEBUG
        LogTool.Debug($"Looking for {key} in {msg.OuterXml}");
#endif
        var result = Lookup(msg, key, path);
        if (result != null)
        {
#if DEBUG
            LogTool.Debug($"Found {key} at {string.Join(", ", path)}");
#endif
            AddKnownPath(key, path);
        }
#if DEBUG
        LogTool.Debug($"Lookup took {stopwatch.Elapsed.TotalSeconds} seconds");
#endif
        return result;
    }

    private XmlNode? Lookup(XmlNode msg, string key, List<int> path)
    {
        if (Validate(msg, key))
        {
            return msg;
        }

        for (int i = 0; i < msg.ChildNodes.Count; i++)
        {
            var child = msg.ChildNodes[i];
            path.Add(i);
            var result = Lookup(child, key, path);
            if (result != null)
            {
                return result;
            }
            path.RemoveAt(path.Count - 1);
        }

        return null;
    }

    private void MovePathToFront(string key, List<int> path)
    {
        var pathKey = GetPathKey(key, path);
        if (_pathNodes.TryGetValue(pathKey, out var node))
        {
            _knownPaths.Remove(node);
            _knownPaths.AddFirst(node);
        }
    }

    private string GetPathKey(string key, List<int> path)
    {
        return $"{key}:{string.Join(",", path)}";
    }
}
