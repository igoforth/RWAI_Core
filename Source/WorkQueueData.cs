namespace AICore;

using System.Collections.Generic;

public interface IWorkItemInterface
{
    public Dictionary<string, object> GetData();
    public string GetType { get; set; }
}
