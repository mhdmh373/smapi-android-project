#!/usr/bin/env python3
"""
ترقيع SMAPI للأندرويد (بدون Cecil write) — يستخدم تعديلاً ثنائياً مباشراً.
يفرغ:
 - AndroidModLoaderManager.StartLoggerToScreen
 - AndroidModLoaderManager.StopLoggerToScreen
 - AndroidModLoaderManager.OnLogImpl
 - SCore.<OnGameInitialized>b__62_0 (خيط الكونسول)
يبقي DeveloperMode كما هو (لزر Share Log).
يحفظ الأصلي بجانبه عند --keep-original.

الاستعمال:
  python3 BinaryPatch.py <input.dll> <output.dll> [--keep-original <orig-copy>]
"""
import sys, shutil, struct, os

def rva_to_offset(sections, rva):
    for va, raw, size, vs in sections:
        if va <= rva < va + max(vs, size):
            return raw + (rva - va)
    return None

def patch(input_path, output_path, keep_original=None):
    try:
        import dnfile
    except ImportError:
        print("dnfile required: pip install dnfile", file=sys.stderr)
        sys.exit(3)
    dn = dnfile.dnPE(input_path)
    sections = [(s.VirtualAddress, s.PointerToRawData, s.SizeOfRawData, s.Misc_VirtualSize) for s in dn.sections]
    targets = []
    for i, m in enumerate(dn.net.mdtables.MethodDef.rows):
        n = str(m.Name)
        if n in ("StartLoggerToScreen","StopLoggerToScreen","OnLogImpl","<OnGameInitialized>b__62_0"):
            targets.append((n, m.Rva))
    if not targets:
        print("No targets found", file=sys.stderr)
        sys.exit(2)
    print(f"Found {len(targets)} methods: {', '.join(n for n,_ in targets)}")
    shutil.copy(input_path, output_path)
    with open(output_path, 'r+b') as f:
        for name, rva in targets:
            off = rva_to_offset(sections, rva)
            if off is None:
                print(f"WARN no offset for {name} RVA {hex(rva)}", file=sys.stderr)
                continue
            f.seek(off)
            b = f.read(1)[0]
            f.seek(off)
            is_tiny = (b & 0x3) == 0x2
            if is_tiny:
                old_sz = b >> 2
                f.seek(off)
                f.write(bytes([0x06, 0x2A]))
                if old_sz > 1:
                    f.write(bytes([0x00]*(old_sz-1)))
                print(f"Patched {name} RVA {hex(rva)} tiny {old_sz}->1")
            else:
                hdr_data = open(output_path,'rb').read()[off:off+8]
                hdr = struct.unpack('<HHI', hdr_data)
                hs = (hdr[0] >> 12)*4
                code_sz = hdr[2]
                total = hs + code_sz
                f.seek(off)
                f.write(bytes([0x06, 0x2A]))
                remaining = total - 2
                if remaining>0:
                    f.write(bytes([0x00]*remaining))
                print(f"Patched {name} RVA {hex(rva)} fat hs={hs} code={code_sz}->1")
    if keep_original:
        shutil.copy(input_path, keep_original)
        print(f"Kept original at {keep_original}")
    print(f"Wrote {output_path} ({os.path.getsize(output_path)} bytes)")
    dn2 = dnfile.dnPE(output_path)
    sections2 = [(s.VirtualAddress, s.PointerToRawData, s.SizeOfRawData, s.Misc_VirtualSize) for s in dn2.sections]
    for i, m in enumerate(dn2.net.mdtables.MethodDef.rows):
        n=str(m.Name)
        if n in ("StartLoggerToScreen","StopLoggerToScreen","OnLogImpl","<OnGameInitialized>b__62_0"):
            off=rva_to_offset(sections2, m.Rva)
            b=open(output_path,'rb').read()[off]
            assert b==0x06, f"verify failed {n} b={hex(b)}"
    print("Verify OK (all patched methods are tiny ret)")

if __name__=="__main__":
    if len(sys.argv)<3:
        print(f"Usage: {sys.argv[0]} <input.dll> <output.dll> [--keep-original <path>]")
        sys.exit(1)
    keep=None
    args=sys.argv[1:]
    inp, outp = args[0], args[1]
    for i in range(2,len(args)):
        if args[i]=="--keep-original" and i+1<len(args):
            keep=args[i+1]
    patch(inp, outp, keep)
