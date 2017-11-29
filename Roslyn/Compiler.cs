﻿namespace Roslyn
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Emit;
    using System.Collections.Generic;

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

        public void StartStuff(IEnumerable<string> references)
        {
            var rs = new List<MetadataReference>();
            foreach(var r in references)
            {
                rs.Add(MetadataReference.CreateFromFile(r));
            }
            References = rs;
            var res = Build("hejmor", Source);
            var newType = res.Item1.ExportedTypes.FirstOrDefault(x => x.Name == "MyClass");
            var programInstance = (IShared) Activator.CreateInstance(newType);
            var methodValue = programInstance.GetAnswer();
            Console.WriteLine("Compiler.StartStuff: {0}", methodValue);
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

        Tuple<Assembly, MetadataReference> Build(string assmName, string source, MetadataReference schema = null)
        {
            var currentAssembly = typeof(Compiler).GetTypeInfo().Assembly;
            var fileUri = "file:///";
            // pretty dumb test for windows platform
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEMP")))
            {
                fileUri = "file://";
            }
            var asmPath = Path.GetFullPath(currentAssembly.CodeBase.Substring(fileUri.Length));

            var compilerOptions = new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary);
            var trees = new SyntaxTree[] {
                CSharpSyntaxTree.ParseText(source),
            };

            var compilation = CSharpCompilation.Create(assmName)
                .WithOptions(compilerOptions)
                .WithReferences(References.Concat(new [] {
                    MetadataReference.CreateFromFile(asmPath) 
                }))
                // .Concat(schema != null ? 
                //     new [] { schema } : new MetadataReference[] {}
                // )))
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
