#!/usr/bin/env python3
"""تحليل زمني لسجل SMAPI (يعمل ببايثون القياسية فقط، مناسب لـ Termux).

الاستعمال:
  python3 scripts/measure_boot.py SMAPI-latest.txt
  python3 scripts/measure_boot.py before.txt after.txt   # مقارنة قبل/بعد

ما يفعله:
  - يقرأ أسطر السجل ذات الطابع الزمني [HH:MM:SS ...] ويحسب المدة الكلية.
  - يعرض أكبر 10 فجوات زمنية بين سطرين متتاليين (أين يقضي الإقلاع وقته).
  - يعد أسطر الخطأ/الاستثناء.
  - عند تمرير ملفين: يقارن المدة الكلية وعدد الأخطاء.

تنبيه: أسماء المراحل في SMAPI تختلف بين النسخ، لذلك نعرض الفجوات مع نص
السطرين المحيطين بدل الادعاء بمرحلة معينة. هذا تحليل إرشادي فقط.
"""
import re
import sys

TS = re.compile(r"^\[(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d{1,3}))?")


def parse(path):
    events = []
    errors = 0
    total_lines = 0
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            total_lines += 1
            low = line.lower()
            if "error" in low or "exception" in low or "crashed" in low or "failed" in low:
                errors += 1
            m = TS.match(line)
            if not m:
                continue
            h, mi, s = int(m.group(1)), int(m.group(2)), int(m.group(3))
            ms = int((m.group(4) or "0").ljust(3, "0")[:3])
            t = ((h * 60 + mi) * 60 + s) * 1000 + ms
            events.append((t, line.strip()[:160]))
    return events, errors, total_lines


def report(name, events, errors, total):
    print(f"=== {name} ===")
    print(f"الأسطر: {total} | أسطر الخطأ/الاستثناء: {errors} | أسطر موقوتة: {len(events)}")
    if len(events) < 2:
        print("لا توجد طوابع زمنية كافية للتحليل.")
        return None
    total_ms = events[-1][0] - events[0][0]
    if total_ms < 0:
        total_ms += 24 * 3600 * 1000  # عبور منتصف الليل
    print(f"المدة الكلية للسجل: {total_ms / 1000:.1f} ثانية")
    gaps = []
    for i in range(1, len(events)):
        d = events[i][0] - events[i - 1][0]
        if d < 0:
            d += 24 * 3600 * 1000
        gaps.append((d, events[i - 1][1], events[i][1]))
    gaps.sort(reverse=True)
    print("أكبر 10 فجوات (أين يذهب الوقت):")
    for k, (d, before, after) in enumerate(gaps[:10], 1):
        print(f"  {k}) {d / 1000:.1f}s")
        print(f"     قبل: {before}")
        print(f"     بعد: {after}")
    return total_ms


def main(paths):
    if len(paths) == 1:
        events, errors, total = parse(paths[0])
        report(paths[0], events, errors, total)
    elif len(paths) == 2:
        results = []
        for p in paths:
            events, errors, total = parse(p)
            results.append(report(p, events, errors, total))
            print()
        if results[0] is not None and results[1] is not None and results[0] > 0:
            delta = (results[1] - results[0]) / results[0] * 100
            print(f"الفرق: {results[1] / 1000:.1f}s مقابل {results[0] / 1000:.1f}s ({delta:+.1f}%)")
            print("انسخ هذه الأرقام مع الملفين عند طلب المقارنة قبل/بعد.")
    else:
        print("الاستعمال: measure_boot.py <log.txt> [after.txt]", file=sys.stderr)
        sys.exit(2)


if __name__ == "__main__":
    main(sys.argv[1:])
