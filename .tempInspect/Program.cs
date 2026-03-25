using System;
using System.Linq;
using System.Reflection;

class Program
{
    static void Main()
    {
        var path = @"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\Common7\IDE\CommonExtensions\Microsoft\Editor\Microsoft.VisualStudio.Language.Intellisense.dll";
        var asm = Assembly.LoadFrom(path);
        Type[] rawTypes;
        try
        {
            rawTypes = asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            rawTypes = ex.Types.Where(t => t != null).ToArray();
        }
        var types = rawTypes.Where(t => t.Name.Contains("QuickInfo") || t.Name.Contains("SuggestedAction") || t.Name.Contains("CodeAction")).OrderBy(t => t.Name).ToList();
        Console.WriteLine("Found types: " + types.Count);
        foreach (var t in types)
        {
            Console.WriteLine(t.FullName);
        }
        return;
    }
}
