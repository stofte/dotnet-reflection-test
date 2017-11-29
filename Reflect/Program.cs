namespace Reflect
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Runtime.Loader;
    using System.Text;
    using Buildalyzer;
    using Entity;
    using Microsoft.Extensions.DependencyModel;
    using Microsoft.Extensions.PlatformAbstractions;
    using Roslyn;

    public interface IShared
    {
        int SomeMethod1(int i);
        string SomeMethod2(int i);
        int SomeMethod3(string str);
    }

    class Program
    {
        
        static void Main(string[] args)
        {
            new Program().StartReflection();
            new Program().StartDynamicMethod();
            new Program().StartCompiler();
        }
        
        void StartReflection()
        {
            var t = BuildType();
            var ms = new MemoryStream();
            var o = Activator.CreateInstance(t, new object[] { ms }) as IShared;
            
            Console.WriteLine("SomeMethod1: {0}", o.SomeMethod1(5));
            Console.WriteLine("SomeMethod2: {0}", o.SomeMethod2(5));
            Console.WriteLine("SomeMethod3: {0}", o.SomeMethod3("hej mor"));
            Console.WriteLine("Stream: {0}", Encoding.Default.GetString(ms.ToArray()));
        }

        public IEnumerable<Assembly> GetReferencingAssemblies(string assemblyName)
        {
            var assemblies = new List<Assembly>();
            var dependencies = DependencyContext.Default.RuntimeLibraries;
            foreach (var library in dependencies)
            {
                var assembly = Assembly.Load(new AssemblyName(library.Name));
                var ts = assembly.ExportedTypes;
                foreach(var t in ts)
                {
                    Console.WriteLine(string.Format("Type: {0}", t.FullName));
                }
                assemblies.Add(assembly);
            }
            return assemblies;
        }

        private static bool IsCandidateLibrary(RuntimeLibrary library, string assemblyName)
        {
            return library.Name == (assemblyName)
                || library.Dependencies.Any(d => d.Name.StartsWith(assemblyName));
        }

        void StartCompiler()
        {
            var loadCtx = AssemblyLoadContext.GetLoadContext(typeof(Program).GetTypeInfo().Assembly);
            var comp = new Compiler(new LibraryLoader(loadCtx));
            comp.StartStuff(GetReferences());
        }

        IEnumerable<string> GetReferences()
        {
            // https://github.com/daveaglick/Buildalyzer
            var projPath = Path.Combine(ProjectPath(), "Entity", "Entity.csproj");
            var manager = new AnalyzerManager();
            var analyzer = manager.GetProject(projPath);
            var refs = analyzer.GetReferences();
            Console.WriteLine("Entity.csproj looks to have about {0} references", refs.Count);
            var list = new List<string>();
            foreach(var r in refs)
            {
                Console.WriteLine("Ref: {0}", r);
                list.Add(r);
            }
            return list;
        }


        string ProjectPath()
        {
            var otherBase = Assembly.GetEntryAssembly().CodeBase;
            Console.WriteLine("Asm CodeBase: {0}", otherBase);
            var basePath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
            // f5
            if (basePath.EndsWith(Path.Combine("Reflect", "bin", "Debug", "netcoreapp2.0")))
            {
                basePath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", ".."));
            }
            else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPVEYOR")))
            {
                basePath = Environment.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER");
            }
            else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TRAVIS")))
            {
                basePath = Environment.GetEnvironmentVariable("TRAVIS_BUILD_DIR");
            }
            return basePath;
        }

        Type BuildType()
        {
            var name = new AssemblyName(Guid.NewGuid().ToString());
            var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndCollect);
            var moduleBuilder = asmBuilder.DefineDynamicModule("MyModule");
            var typeMods = TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit;
            var ctorMods = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;

            var typeBuilder = moduleBuilder.DefineType("Program", typeMods, typeof(Object), new [] { typeof(IShared) });
            var streamFieldBuilder = typeBuilder.DefineField("_stream", typeof(Stream), FieldAttributes.Private);
            
            var ctorBuilder = typeBuilder.DefineConstructor(ctorMods, CallingConventions.Standard, new[] { typeof(Stream) });
            var ctorIL = ctorBuilder.GetILGenerator();
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Call, typeof(Object).GetConstructor(Type.EmptyTypes));
            ctorIL.Emit(OpCodes.Ldarg_0);
            ctorIL.Emit(OpCodes.Ldarg_1); // stream
            ctorIL.Emit(OpCodes.Stfld, streamFieldBuilder);
            ctorIL.Emit(OpCodes.Ret);
            BuildSimpleMethod(typeBuilder);
            BuildSimpleNumberMethod(typeBuilder);
            BuildStreamMethod(typeBuilder, streamFieldBuilder);
            var t = typeBuilder.CreateType();
            return t;
        }

        void BuildSimpleMethod(TypeBuilder typeBuilder)
        {
            var methodFlags = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot;
            var methodBuilder = typeBuilder.DefineMethod("SomeMethod2", methodFlags, CallingConventions.Standard, typeof(string), new[] { typeof(int) });
            var intToStringMethod = typeof(int).GetMethod("ToString", Type.EmptyTypes);
            var mil = methodBuilder.GetILGenerator();
            // docs claim args are from idx 0, but assume 0 is the "this" reference
            mil.Emit(OpCodes.Ldarga_S, 1);
            mil.Emit(OpCodes.Call, intToStringMethod);
            mil.Emit(OpCodes.Ret);
        }

        void BuildSimpleNumberMethod(TypeBuilder typeBuilder)
        {
            var methodFlags = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot;
            var methodBuilder = typeBuilder.DefineMethod("SomeMethod1", methodFlags, CallingConventions.Standard, typeof(int), new[] { typeof(int) });
            var mil = methodBuilder.GetILGenerator();
            var endOfBody = mil.DefineLabel();
            // returns argument value directly
            mil.Emit(OpCodes.Ldarg_1);
            mil.Emit(OpCodes.Ret);
        }

        void BuildStreamMethod(TypeBuilder typeBuilder, FieldBuilder streamField)
        {
            var stringLengthM = typeof(string).GetProperty("Length").GetMethod;
            var stringGetCharsM = typeof(string).GetMethod("get_Chars");
            var streamWriteByteM = typeof(Stream).GetMethod("WriteByte");
            var convertToBytesM = typeof(Convert).GetMethod("ToByte", new[] { typeof(char) });

            // for some reason, NewSlot and Virtual are required, even if the call interface looks similar to 
            var methodFlags = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual;
            var methodBuilder = typeBuilder.DefineMethod("SomeMethod3", methodFlags, CallingConventions.Standard, typeof(int), new[] { typeof(string) });
            var mil = methodBuilder.GetILGenerator();
            var loopStart = mil.DefineLabel();
            var loopBody = mil.DefineLabel();

            mil.DeclareLocal(typeof(int)); // local 0 => index
            mil.DeclareLocal(typeof(int)); // local 1 => length

            mil.Emit(OpCodes.Ldc_I4_0);
            mil.Emit(OpCodes.Stloc_0);

            mil.Emit(OpCodes.Ldarg_1); // str
            mil.Emit(OpCodes.Callvirt, stringLengthM);
            mil.Emit(OpCodes.Stloc_1); // length

            mil.Emit(OpCodes.Ldc_I4_0);
            mil.Emit(OpCodes.Stloc_0); // index

            mil.Emit(OpCodes.Br_S, loopStart);

            mil.MarkLabel(loopBody);
            mil.Emit(OpCodes.Ldarg_0); // this
            mil.Emit(OpCodes.Ldfld, streamField);
            mil.Emit(OpCodes.Ldarg_1); // str
            mil.Emit(OpCodes.Ldloc_0); // index
            mil.Emit(OpCodes.Callvirt, stringGetCharsM);
            mil.Emit(OpCodes.Call, convertToBytesM);
            mil.Emit(OpCodes.Callvirt, streamWriteByteM);

            // for-loop post-body increment
            mil.Emit(OpCodes.Ldloc_0); // index
            mil.Emit(OpCodes.Ldc_I4_1);
            mil.Emit(OpCodes.Add);
            mil.Emit(OpCodes.Stloc_0); // index
            
            mil.MarkLabel(loopStart); // for-loop conditional
            mil.Emit(OpCodes.Ldloc_0);
            mil.Emit(OpCodes.Ldloc_1);
            mil.Emit(OpCodes.Blt_S, loopBody);

            mil.Emit(OpCodes.Ldloc_0);
            mil.Emit(OpCodes.Ret);
        }


        // https://docs.microsoft.com/en-us/dotnet/framework/reflection-and-codedom/how-to-define-and-execute-dynamic-methods
        private delegate TReturn OneParameter<TReturn, TParameter0>(TParameter0 p0);
        
        void StartDynamicMethod()
        {
            Type[] methodArgs = { typeof(int) };
            DynamicMethod squareIt = new DynamicMethod("SquareIt", typeof(long), methodArgs, typeof(Program).Module);
            ILGenerator il = squareIt.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Conv_I8);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Mul);
            il.Emit(OpCodes.Ret);
            OneParameter<long, int> invokeSquareIt = (OneParameter<long, int>)squareIt.CreateDelegate(typeof(OneParameter<long, int>));
            var result = invokeSquareIt(42);
            Console.WriteLine("42 squared = {0}", result);
        }
    }
}
