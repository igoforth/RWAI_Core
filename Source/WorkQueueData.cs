using System.Collections.Generic;

namespace AICore;

public interface IWorkItemInterface
{
    public Dictionary<string, object> GetData();
    public string GetType { get; set; }
}
