using System;
using System.IO;
using System.Reflection;

namespace Roslyn
{
    public interface ILibraryLoader
    {
        Assembly LoadFromStream(Stream stream);
    }
}