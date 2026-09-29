# دليل القياس (م1) — خطوات قصيرة للهاتف

> تُنفَّذ كلها في النهاية دفعة واحدة. لا ترسل عشرات الملفات: ملف واحد فقط عند كل خطوة.

## أين السجلات
- سجل SMAPI: بجانب المودات في `smapi-internal/` (ملف `SMAPI-*.txt` الأحدث).
- المودات: `/storage/emulated/0/Android/data/abc.smapi.gameloader/files/Mods`
- عند الانهيار: آخر 15 سطرًا من السجل تكفي مبدئيًا.

## 1) زمن الإقلاع (Termux أو كمبيوتر)
```
python3 scripts/measure_boot.py SMAPI-latest.txt
```
انسخ: المدة الكلية + أكبر 3 فجوات. للمقارنة قبل/بعد:
```
python3 scripts/measure_boot.py before.txt after.txt
```

## 2) الاختبار التدريجي
```
python3 scripts/measure_mods.py --plan Mods
python3 scripts/measure_mods.py --apply 20 Mods
```
شغّل اللعبة → سجّل → كرر مع 50 ثم 100. للتراجع الكامل:
```
python3 scripts/measure_mods.py --restore Mods
```

## 3) عند أي انهيار/تجمّد
```
python3 scripts/classify_crash.py SMAPI-latest.txt
```
انسخ: سطر النتيجة + آخر 15 سطرًا. لا حاجة لـ logcat إلا إذا طُلب.

## 4) الذاكرة (يدوي، سطر واحد)
- أثناء اللعب: `adb shell dumpsys meminfo abc.smapi.gameloader` (من كمبيوتر)، أو من الهاتف: إعدادات المطور ← خدمات قيد التشغيل.
- انسخ رقم `TOTAL PSS` فقط.

## ما ترسله في النهاية (4 أشياء فقط)
1. موديل الهاتف والرام — **وصل: Samsung M52 / 8GB** ✓
2. نسخة SVE من `manifest.json` (رقم `Version` فقط) — **ما زال مطلوبًا**
3. ناتج `measure_boot` عند 20/50/100 مود (3 أسطر)
4. ناتج `classify_crash` عند أول انهيار (إن وجد)
