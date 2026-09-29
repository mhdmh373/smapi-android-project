#!/usr/bin/env python3
"""مصنّف أعطال SMAPI: يقرأ ملف سجل (أو مقتطف logcat) ويقترح نوع المشكلة.

الاستعمال:
  python3 scripts/classify_crash.py SMAPI-latest.txt
  adb logcat -d | python3 scripts/classify_crash.py

الفئات: نفاد ذاكرة / ANR / عدم توافق مود / خطأ SveFix / تبعية ناقصة /
Harmony على هدف مفقود / غير معروف (يعرض آخر 15 سطرًا).

كل نتيجة مرفقة بسبب مقترح وخطوة تالية واحدة فقط. التشخيص إرشادي.
"""
import re
import sys


def read_input():
    if len(sys.argv) > 1:
        with open(sys.argv[1], encoding="utf-8", errors="replace") as f:
            return f.read()
    return sys.stdin.read()


RULES = [
    ("نفاد ذاكرة",
     [r"outofmemory", r"failed to allocate", r"grow heap", r"throwing outofmemory",
      r"lowmemorykiller", r"lmkd.*kill", r"exceeds.*memory"],
     "اللعبة قُتلت لقلة الذاكرة. الخطوة: قلل عدد المودات الثقيلة (خرائط/صور) ثم أعد القياس بـ measure_boot.py."),
    ("ANR (تجمّد الواجهة)",
     [r"\banr\b", r"input dispatching timed out", r"isn't responding",
      r"applicationexitinfo.*anr", r"waiting because.*main.*thread"],
     "خيط الواجهة محجوب. الخطوة: أرسل مقتطف الـ logcat حول كلمة ANR مع اسم المود الذي كان يُحمَّل لحظتها."),
    ("عدم توافق SveFix/SVE",
     [r"svefix", r"tmxl.*facing", r"flashshifter", r"stardewvalleyexpanded"],
     "إصلاح التوافق المدمج تعارض مع نسخة SVE. الخطوة: أرسل رقم نسخة SVE من manifest.json."),
    ("مود غير متوافق مع نسخة اللعبة",
     [r"missingmethodexception", r"missingfieldexception", r"typeloadexception",
      r"method not found", r"field not found", r"pagesofcraftingrecipes"],
     "مود يطلب دالة/حقلًا غير موجود في 1.6.15. الخطوة: حدّث المود أو عطّله مؤقتًا."),
    ("تبعية ناقصة",
     [r"could not load file or assembly", r"filenotfoundexception", r"dependency.*missing",
      r"requires.*not.*install"],
     "مود ينقصه مود مساعد. الخطوة: ثبّت التبعية المذكورة في السطر نفسه."),
    ("Harmony على هدف مفقود",
     [r"null method", r"harmony.*null", r"original.*not found", r"patch.*failed"],
     "ترقيع Harmony يشير لهدف تغيّر اسمه. الخطوة: انسخ كتلة الخطأ كاملة (10 أسطر قبل/بعد)."),
    ("انهيار المكدس/حلقة ترقيع",
     [r"stackoverflow", r"recursive.*patch", r"infinite loop"],
     "احتمال حلقة بين ترقيعين. الخطوة: عطّل آخر مود أُضيف (الوضع الآمن) وأعد التشغيل."),
]


def main():
    text = read_input()
    low = text.lower()
    hits = []
    for title, patterns, advice in RULES:
        matched = [p for p in patterns if re.search(p, low)]
        if matched:
            hits.append((title, matched[0], advice))
    if not hits:
        print("النتيجة: غير معروف — لم تُطابق أي قاعدة.")
    else:
        print(f"النتيجة: {len(hits)} إشارة:")
        for title, pat, advice in hits:
            print(f"- {title} (الدليل: `{pat}`)")
            print(f"  {advice}")
    print()
    print("--- آخر 15 سطرًا (انسخها مع تقريرك) ---")
    lines = text.strip().splitlines()
    for line in lines[-15:]:
        print(line[:220])


if __name__ == "__main__":
    main()
