#!/usr/bin/env python3
"""
بناء الزيبات بالبنية المقبولة من اللانشر.

اللانشر يرفض أي زيب فيه "مدخلات مجلدات" (directory entries) أو بنية مختلفة عن الأصلي.
الزيب المقبول: نفس ملفات الزيب الأصلي بنفس الأسماء والترتيب، بدون مدخلات مجلدات، الضغط deflate.
(يمكن بناؤه بـ Python zipfile مع نسخ أسماء وترتيب الملفات من الأصلي.)

- SMAPI: يبني من الزيب الأصلي (الـ .txt المرفوع) بنسخ ترتيب وأسماء ملفاته وإدخال StardewModdingAPI.dll المرقّع بدل الأصلي.
- BigInventory: يبني زيب مود مستقل بملفين (BigInventory/manifest.json + BigInventory/BigInventory.dll) بدون مجلدات، deflate.

الفحوصات (كل ملف zip يجب أن يجتازها):
  - لا مدخلات مجلدات
  - ترتيب الأسماء مطابق للأصلي (لزيب SMAPI)
  - testzip بلا أخطاء
  - python يقرأ manifest.json كـ JSON صالح وفيه EntryDll (للمودات)

الاستعمال:
  python3 scripts/build_zips.py --smapi-orig SMAPI-Android-4.3.2.5-(...).txt --smapi-patched-dll patched.dll --out-smapi dist/SMAPI-Android-4_3_2_5-patched.zip
  python3 scripts/build_zips.py --biginventory-dll src/BigInventory/BigInventory.dll --out-biginventory dist/BigInventory.zip
  python3 scripts/build_zips.py --verify dist/SMAPI-...zip --verify-biginventory dist/BigInventory.zip
"""
import argparse, json, os, sys, zipfile, hashlib, pathlib

def order_from_zip(zip_path):
    z = zipfile.ZipFile(zip_path, 'r')
    infos = z.infolist()
    names = [i.filename for i in infos if not i.is_dir()]
    dir_entries = [i.filename for i in infos if i.is_dir()]
    return names, dir_entries, infos

def build_smapi_zip(orig_zip_path, patched_dll_path, out_path):
    names, dir_entries, infos = order_from_zip(orig_zip_path)
    if dir_entries:
        print(f"WARN orig has dir entries (unexpected): {dir_entries}", file=sys.stderr)
    # read all orig files into memory map
    orig = zipfile.ZipFile(orig_zip_path, 'r')
    patched_bytes = open(patched_dll_path, 'rb').read()
    out_dir = os.path.dirname(os.path.abspath(out_path))
    os.makedirs(out_dir, exist_ok=True)
    with zipfile.ZipFile(out_path, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as out_zip:
        for name in names:
            if name.endswith("StardewModdingAPI.dll"):
                data = patched_bytes
            else:
                data = orig.read(name)
            # اكتب كـ file entry فقط (لا مجلد) — ZipInfo بدون is_dir
            zi = zipfile.ZipInfo(filename=name)
            zi.compress_type = zipfile.ZIP_DEFLATED
            # الحفاظ على تاريخ الإصدار الأصلي إن وُجد
            orig_info = next((x for x in infos if x.filename == name), None)
            if orig_info:
                zi.date_time = orig_info.date_time
            out_zip.writestr(zi, data)
    print(f"Built SMAPI zip: {out_path} ({os.path.getsize(out_path)} bytes, {len(names)} files)")
    return verify_smapi_zip(out_path, orig_zip_path)

def build_biginventory_zip(dll_path, manifest_path, out_path):
    # BigInventory zip: مجلد واحد اسمه BigInventory يحوي ملفين فقط
    manifest_bytes = open(manifest_path, 'rb').read()
    # verify manifest
    j = json.loads(manifest_bytes.decode('utf-8'))
    if "EntryDll" not in j:
        print(f"ERROR manifest.json missing EntryDll: {j}", file=sys.stderr)
        sys.exit(4)
    dll_bytes = open(dll_path, 'rb').read()
    out_dir = os.path.dirname(os.path.abspath(out_path))
    os.makedirs(out_dir, exist_ok=True)
    with zipfile.ZipFile(out_path, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for arcname, data in [("BigInventory/manifest.json", manifest_bytes), ("BigInventory/BigInventory.dll", dll_bytes)]:
            zi = zipfile.ZipInfo(filename=arcname)
            zi.compress_type = zipfile.ZIP_DEFLATED
            zi.date_time = (2026, 9, 29, 0, 0, 0)
            z.writestr(zi, data)
    print(f"Built BigInventory zip: {out_path} ({os.path.getsize(out_path)} bytes)")
    return verify_biginventory_zip(out_path)

def verify_smapi_zip(zip_path, orig_zip_path=None):
    z = zipfile.ZipFile(zip_path, 'r')
    infos = z.infolist()
    dir_entries = [i.filename for i in infos if i.is_dir()]
    if dir_entries:
        print(f"FAIL {zip_path}: has directory entries: {dir_entries}", file=sys.stderr)
        return False
    err = z.testzip()
    if err is not None:
        print(f"FAIL {zip_path}: testzip error at {err}", file=sys.stderr)
        return False
    if orig_zip_path:
        orig_names, _, _ = order_from_zip(orig_zip_path)
        names = [i.filename for i in infos]
        if names != orig_names:
            print(f"FAIL {zip_path}: order mismatch", file=sys.stderr)
            print(f"  orig: {orig_names[:5]} ... ({len(orig_names)})", file=sys.stderr)
            print(f"  out : {names[:5]} ... ({len(names)})", file=sys.stderr)
            return False
    print(f"OK {zip_path}: no dir entries, order OK, testzip OK, {len(infos)} files")
    return True

def verify_biginventory_zip(zip_path):
    z = zipfile.ZipFile(zip_path, 'r')
    infos = z.infolist()
    if any(i.is_dir() for i in infos):
        print(f"FAIL {zip_path}: has directory entries", file=sys.stderr)
        return False
    if z.testzip() is not None:
        print(f"FAIL {zip_path}: testzip failed", file=sys.stderr)
        return False
    # manifest valid JSON + EntryDll
    try:
        j = json.loads(z.read("BigInventory/manifest.json").decode('utf-8'))
        if "EntryDll" not in j:
            print(f"FAIL {zip_path}: manifest missing EntryDll", file=sys.stderr)
            return False
        if j["EntryDll"] != "BigInventory.dll":
            print(f"WARN {zip_path}: EntryDll={j['EntryDll']} (expected BigInventory.dll)", file=sys.stderr)
    except Exception as e:
        print(f"FAIL {zip_path}: manifest read error: {e}", file=sys.stderr)
        return False
    print(f"OK {zip_path}: BigInventory zip valid, EntryDll={j['EntryDll']}, files={len(infos)}")
    return True

def main():
    ap = argparse.ArgumentParser(description="Build launcher-compatible zips")
    ap.add_argument("--smapi-orig", dest="smapi_orig", help="Path to original SMAPI zip (.txt renamed .zip)")
    ap.add_argument("--smapi-patched-dll", dest="smapi_dll", help="Path to patched StardewModdingAPI.dll")
    ap.add_argument("--out-smapi", dest="out_smapi", help="Output SMAPI zip path")
    ap.add_argument("--biginventory-dll", dest="bi_dll", help="Path to BigInventory.dll")
    ap.add_argument("--biginventory-manifest", dest="bi_manifest", help="Path to manifest.json (default src/BigInventory/manifest.json)")
    ap.add_argument("--out-biginventory", dest="out_bi", help="Output BigInventory zip path")
    ap.add_argument("--verify", dest="verify", help="Verify a SMAPI zip at path")
    ap.add_argument("--verify-biginventory", dest="verify_bi", help="Verify a BigInventory zip at path")
    ap.add_argument("--orig-for-verify", dest="orig_for_verify", help="Orig zip to compare order when verifying SMAPI zip")
    args = ap.parse_args()
    ok = True
    if args.smapi_orig and args.smapi_dll and args.out_smapi:
        ok &= build_smapi_zip(args.smapi_orig, args.smapi_dll, args.out_smapi)
    if args.bi_dll and args.out_bi:
        manifest = args.bi_manifest or "src/BigInventory/manifest.json"
        ok &= build_biginventory_zip(args.bi_dll, manifest, args.out_bi)
    if args.verify:
        ok &= verify_smapi_zip(args.verify, args.orig_for_verify or args.smapi_orig)
    if args.verify_bi:
        ok &= verify_biginventory_zip(args.verify_bi)
    sys.exit(0 if ok else 1)

if __name__ == "__main__":
    main()
