namespace Roslyn
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Emit;
    using System.Collections.Generic;
    using System.Text;

    public interface IEFQuery
    {
        int Run();
    }


    public interface IShared
    {
        int GetAnswer();
    }

    public class Compiler
    {
        ILibraryLoader _libraryLoader;

        public Compiler(ILibraryLoader libraryLoader)
        {
            _libraryLoader = libraryLoader;
        }

        IEnumerable<MetadataReference> References;

        static int i = 0;
        public void StartStuff(IEnumerable<string> references)
        {
            var totalBytes = SetReferences(references);
            var res = Build("hejmor" + (i++).ToString(), Source); // ensure each generated asm has unique names
            var newType = res.Item1.ExportedTypes.FirstOrDefault(x => x.Name == "MyClass");
            var programInstance = (IShared) Activator.CreateInstance(newType);
            var methodValue = programInstance.GetAnswer();
            Console.WriteLine("Compiler.StartStuff: generated return val => {0}", methodValue);
            Console.WriteLine("Compiler.StartStuff: metadata mb => {0}", totalBytes / 1024d / 1024d);
        }

        string Source = @"
using System;
using Roslyn;

public class MyClass : IShared
{
    public int GetAnswer()
    {
        Console.WriteLine(""Hello World!"");
        return 42;
    }   
}";

        public int SetReferences(IEnumerable<string> references)
        {
            Environment.Exit(0);
            var totalBytes = 0;
            var rs = new List<MetadataReference>();
            foreach(var r in references)
            {
                Console.WriteLine("MetaRef: {0}", r);
                // first, copy the file to some random place
                var newFile = Path.GetTempFileName();
                File.Copy(r, newFile, true);
                var bytes = File.ReadAllBytes(newFile);
                File.Delete(newFile);
                totalBytes += bytes.Length;
                var stream = new MemoryStream(bytes);
                rs.Add(MetadataReference.CreateFromStream(stream));
            }
            References = rs;
            return totalBytes;
        }

        public Tuple<Assembly, MetadataReference> Build(string assmName, string source, MetadataReference schema = null)
        {
            var compilerOptions = new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary);
            var trees = new SyntaxTree[] {
                CSharpSyntaxTree.ParseText(source),
            };

            var schemaRef = schema != null ? new [] { schema } : new MetadataReference[] {};
                // .Concat(schema != null ? 
                //     new [] { schema } : new MetadataReference[] {}
                // )))


            var allReferences = schemaRef.Concat(References);

            var compilation = CSharpCompilation.Create(assmName)
                .WithOptions(compilerOptions)
                .WithReferences(allReferences)
                .AddSyntaxTrees(trees);

            var stream = new MemoryStream();
            var compilationResult = compilation.Emit(stream, options: new EmitOptions());
            foreach(var diag in compilationResult.Diagnostics)
            {
                if (diag.Severity == DiagnosticSeverity.Error)
                {
                    Console.WriteLine("Error: {0}", diag.GetMessage());
                }
            }
            stream.Position = 0;
            var asm = _libraryLoader.LoadFromStream(stream);
            stream.Position = 0;
            var metaRef = MetadataReference.CreateFromStream(stream);
            return Tuple.Create(asm, metaRef as MetadataReference);
        }

        public IEnumerable<MetadataReference> GetReferences()
        {
            var adws = new AdhocWorkspace();
            var refs = adws.CurrentSolution.Projects.SelectMany(x => x.MetadataReferences);
            return new MetadataReference[] { };
        }
    }
}
