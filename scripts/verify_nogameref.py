#!/usr/bin/env python3
"""فحص ساكن: موداتنا بلا أي مرجع ترجمة لتجميعات اللعبة.

يفحص:
  1) المصدر: لا using ولا Reference لـ StardewValley/MonoGame/xTile/Harmony.
  2) الـ DLL الناتج: لا يوجد اسم تجميع ASCII لـ MonoGame/Harmony/xTile/
     StardewValley في جدول مراجع التجميعات (أسماء #Strings تكون ASCII،
     بينما سلاسل البحث وقت التشغيل مخزنة UTF-16 فلا تظهر هنا).

الاستعمال:
  python3 scripts/verify_nogameref.py src/SmartKeyboard dist/SmartKeyboard/SmartKeyboard.dll
"""
import re
import sys

BANNED = ["StardewValley", "MonoGame", "xTile", "Harmony", "Microsoft.Xna"]


def check_source(src_dir):
    import os
    bad = []
    for root, _, files in os.walk(src_dir):
        for fn in files:
            if not fn.endswith(".cs") and not fn.endswith(".csproj"):
                continue
            path = os.path.join(root, fn)
            text = open(path, encoding="utf-8", errors="replace").read()
            # أزل التعليقات قبل الفحص (التوثيق قد يذكر الأسماء للتحذير منها)
            text = re.sub(r"<!--.*?-->", "", text, flags=re.DOTALL)
            text = re.sub(r"//.*", "", text)
            for b in BANNED:
                # using X / Reference Include="X / HintPath ...X
                if re.search(r"(using\s+[\w.]*" + re.escape(b) + r")|"
                             r"(Reference\s+Include=\"" + re.escape(b) + r")|"
                             r"(" + re.escape(b) + r"[\w.]*\.dll)", text):
                    bad.append(f"{path}: references {b}")
    return bad


def check_dll(dll_path):
    data = open(dll_path, "rb").read()
    # أسماء التجميعات المرجعية تُخزن ASCII في #Strings؛ نبحث عنها كـ ASCII
    # مع حدود كلمات لتجنب الإيجابيات الكاذبة داخل UTF-16.
    bad = []
    ascii_text = data.decode("ascii", errors="ignore")
    for b in BANNED:
        for m in re.finditer(re.escape(b), ascii_text):
            # تجاهل ما هو جزء من UTF-16 (محاط ببايتات null) — لا، ASCII decode
            # يحوّل null إلى \x00 ونحن نبحث في النص المفكك، فأي تطابق ASCII
            # حقيقي هنا يعني اسمًا في #Strings أو اسم نوع TypeRef.
            bad.append(f"{dll_path}: ASCII match '{b}' at offset {m.start()}")
    return bad


def main():
    src_dir, dll_path = sys.argv[1], sys.argv[2]
    bad = check_source(src_dir) + check_dll(dll_path)
    # الاستثناء المشروع الوحيد: سلسلة "StardewValley.Game1, Stardew Valley"
    # لازمة للبحث وقت التشغيل (UTF-16، لا تظهر في فحص ASCII أصلًا).
    if bad:
        print("FAIL: game assembly references found:")
        for b in bad[:20]:
            print("  " + b)
        sys.exit(1)
    print(f"OK: {dll_path} has no ASCII game-assembly references; source is clean.")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: verify_nogameref.py <srcDir> <dll>", file=sys.stderr)
        sys.exit(2)
    main()
