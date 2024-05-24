using System.Reflection;

public class TypeResolver
{
    public static void Initialize()
    {
        AppDomain.CurrentDomain.TypeResolve += OnTypeResolve;
    }

    private static Assembly OnTypeResolve(object sender, ResolveEventArgs args)
    {
        // Extract the name of the type being requested
        string requestedTypeName = args.Name;

        // Check if the requested type is one you want to resolve manually
        if (requestedTypeName.Contains("ProtoBuf.Meta.TypeModel"))
        {
            // Load the specific assembly that contains the type
            return Assembly.LoadFrom(@"path\to\correct\version\UnityEngine.UI.dll");
        }
        if (
            requestedTypeName.Contains("System.ValueTuple`5")
            || requestedTypeName.Contains("System.ValueTuple`4")
        )
        {
            // Load the specific assembly that contains the type
            return Assembly.LoadFrom(@"path\to\correct\version\System.ValueTuple.dll");
        }

        // For other types, use the default resolution process
        return null;
    }
}
