#!/usr/bin/env python3
"""اختبار تدريجي للمودات: 20 ثم 50 ثم 100 (قابل للعكس بالكامل).

الاستعمال (Termux داخل مجلد Mods أو بتمرير المسار):
  python3 measure_mods.py --plan /path/to/Mods
  python3 measure_mods.py --apply 50 /path/to/Mods     # يفعّل أول 50 فقط
  python3 measure_mods.py --restore /path/to/Mods      # يعيد كل شيء

--apply ينقل المودات الزائدة إلى مجلد بجانب Mods اسمه
Mods.disabled_<العدد>/ ويحفظ سجلًا (record.json) للتراجع.
--restore يعيدها كلها. لا يحذف شيئًا أبدًا.

الترتيب الافتراضي أبجدي (ثابت بين التجارب). --order size يرتب بالأخف أولًا.
"""
import json
import os
import shutil
import sys

STAGES = (20, 50, 100)


def list_mods(mods_dir):
    mods = []
    for name in sorted(os.listdir(mods_dir)):
        full = os.path.join(mods_dir, name)
        if not os.path.isdir(full):
            continue
        if name.startswith("Mods.disabled_"):
            continue
        size = 0
        for root, _, files in os.walk(full):
            for fn in files:
                try:
                    size += os.path.getsize(os.path.join(root, fn))
                except OSError:
                    pass
        mods.append((name, size))
    return mods


def plan(mods_dir, order):
    mods = list_mods(mods_dir)
    if order == "size":
        mods.sort(key=lambda m: m[1])
    print(f"المودات: {len(mods)} في {mods_dir}")
    for n in STAGES:
        active = [m[0] for m in mods[:n]]
        print(f"--- مرحلة {n}: تفعيل {len(active)} (تعطيل {len(mods) - len(active)}) ---")
        for m in active[:5]:
            print(f"    + {m}")
        if len(active) > 5:
            print(f"    ... (+{len(active) - 5} أخرى)")
    print("نفّذ: --apply 20 ثم شغّل اللعبة وسجّل، ثم --apply 50، ثم --apply 100.")
    print("بعد كل مرحلة: شغّل measure_boot.py على السجل وclassify_crash.py عند أي انهيار.")


def apply_limit(mods_dir, n, order):
    mods = list_mods(mods_dir)
    if order == "size":
        mods.sort(key=lambda m: m[1])
    else:
        mods.sort(key=lambda m: m[0])
    keep = {m[0] for m in mods[:n]}
    disabled_dir = os.path.join(os.path.dirname(mods_dir.rstrip(os.sep)), f"Mods.disabled_{n}")
    os.makedirs(disabled_dir, exist_ok=True)
    record = {"mods_dir": mods_dir, "kept": sorted(keep), "moved": []}
    for name, _ in mods:
        if name in keep:
            continue
        src = os.path.join(mods_dir, name)
        dst = os.path.join(disabled_dir, name)
        if os.path.exists(dst):
            shutil.rmtree(dst)
        shutil.move(src, dst)
        record["moved"].append(name)
    with open(os.path.join(disabled_dir, "record.json"), "w", encoding="utf-8") as f:
        json.dump(record, f, ensure_ascii=False, indent=1)
    print(f"فعّلت {len(keep)} مودًا. نُقل {len(record['moved'])} إلى {disabled_dir}")
    print("للتراجع: measure_mods.py --restore " + mods_dir)


def restore(mods_dir):
    parent = os.path.dirname(mods_dir.rstrip(os.sep))
    restored = 0
    for name in sorted(os.listdir(parent)):
        if not name.startswith("Mods.disabled_"):
            continue
        d = os.path.join(parent, name)
        for mod in os.listdir(d):
            if mod == "record.json":
                continue
            src = os.path.join(d, mod)
            dst = os.path.join(mods_dir, mod)
            if os.path.exists(dst):
                shutil.rmtree(dst)
            shutil.move(src, dst)
            restored += 1
    print(f"أُعيد {restored} مودًا إلى {mods_dir} (مجلدات التعطيل الفارغة تبقى، احذفها يدويًا).")


def main(argv):
    order = "alpha"
    args = [a for a in argv if not a.startswith("--order")]
    if any(a.startswith("--order") for a in argv):
        for a in argv:
            if a.startswith("--order"):
                order = a.split("=", 1)[1]
    if len(args) < 2:
        print("الاستعمال: measure_mods.py [--order=alpha|size] --plan|--apply N|--restore <ModsDir>")
        sys.exit(2)
    cmd = args[0]
    if cmd == "--plan":
        plan(args[1], order)
    elif cmd == "--apply":
        apply_limit(args[2], int(args[1]), order)
    elif cmd == "--restore":
        restore(args[1])
    else:
        print("أمر غير معروف.", file=sys.stderr)
        sys.exit(2)


if __name__ == "__main__":
    main(sys.argv[1:])
