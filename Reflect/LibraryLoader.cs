namespace Reflect
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Runtime.Loader;

    public class LibraryLoader : Roslyn.ILibraryLoader
    {
        AssemblyLoadContext _loadContext;

        public LibraryLoader(AssemblyLoadContext loadContext)
        {
            _loadContext = loadContext;
        }

        public Assembly LoadFromStream(Stream assemblyStream) 
        {

            return _loadContext.LoadFromStream(assemblyStream);
        }
    }
}