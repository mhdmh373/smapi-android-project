using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Linq;
using System.IO;
/// <summary>
/// ترقيع بديل عبر Mono.Cecil (قد يفشل كتابةً على بيئة تفتقد MonoGame.Framework).
/// يُبقي كمرجع، الأداة الفعلية هي BinaryPatch.py.
/// </summary>
class SmapiPatchCecil
{
    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("Usage: SmapiPatchCecil <input.dll> <output.dll> [--keep-original <orig>]"); return 1; }
        string input=args[0], output=args[1]; string keep=null;
        for(int i=2;i<args.Length;i++) if(args[i]=="--keep-original" && i+1<args.Length) keep=args[++i];
        var resolver=new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(input));
        var rp=new ReaderParameters{AssemblyResolver=resolver, InMemory=true};
        var asm=AssemblyDefinition.ReadAssembly(input, rp);
        bool changed=false;
        var loader=asm.MainModule.Types.FirstOrDefault(t=>t.FullName=="StardewModdingAPI.Mobile.AndroidModLoaderManager");
        if(loader!=null) foreach(var n in new[]{"StartLoggerToScreen","StopLoggerToScreen","OnLogImpl"}){
            var m=loader.Methods.FirstOrDefault(x=>x.Name==n);
            if(m!=null && m.HasBody){ m.Body.Instructions.Clear(); m.Body.Variables.Clear(); m.Body.ExceptionHandlers.Clear(); m.Body.GetILProcessor().Emit(OpCodes.Ret); changed=true; Console.WriteLine($"Patched {n}"); }
        }
        var score=asm.MainModule.Types.FirstOrDefault(t=>t.Name=="SCore");
        if(score!=null){ var target=score.Methods.FirstOrDefault(m=>m.Name=="<OnGameInitialized>b__62_0"); if(target!=null && target.HasBody){ target.Body.Instructions.Clear(); target.Body.Variables.Clear(); target.Body.ExceptionHandlers.Clear(); target.Body.GetILProcessor().Emit(OpCodes.Ret); changed=true; Console.WriteLine($"Patched {target.Name}"); } }
        if(!changed){Console.WriteLine("Nothing patched"); return 2;}
        if(keep!=null) File.Copy(input, keep, true);
        asm.Write(output); Console.WriteLine($"Wrote {output}"); return 0;
    }
}
