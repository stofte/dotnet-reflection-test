namespace back
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Text;
    
    public interface IShared
    {
        int SomeMethod1(int i);
        string SomeMethod2(int i);
        int SomeMethod3(string str);
    }

    class Program
    {
        private delegate long SquareItInvoker(int input);
        private delegate TReturn OneParameter<TReturn, TParameter0>(TParameter0 p0);
        
        void Start2()
        {
            var t = BuildType();
            var ms = new MemoryStream();
            var o = Activator.CreateInstance(t, new object[] { ms }) as IShared;
            
            Console.WriteLine("SomeMethod1: {0}", o.SomeMethod1(5));
            Console.WriteLine("SomeMethod2: {0}", o.SomeMethod2(5));
            Console.WriteLine("SomeMethod3: {0}", o.SomeMethod3("hej mor"));
            Console.WriteLine("Stream: {0}", Encoding.Default.GetString(ms.ToArray()));
        }

        static void Main(string[] args)
        {
            new Program().Start2();
            new Program().BuildDyn();
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

        void BuildDyn()
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
            Console.WriteLine("123456789 squared = {0}", invokeSquareIt(123456789));
        }
    }
}
