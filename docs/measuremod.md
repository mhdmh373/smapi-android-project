# Measure — مود القياس على الجهاز

> **تحقق ساكن فقط** حتى تجربه. يستخدم واجهات SMAPI العامة فقط.

## التثبيت
Mod Manager ← Install Mod ← `MeasureMod-v1.zip`. لا إعدادات.

## ماذا يقول لك
افتح كونسول SMAPI واكتب:
- `measure` — التقرير: مراحل الإقلاع بالثواني + عدد التجميدات (فجوات فوق ثانيتين)
  وأطول فجوة + عينات الذاكرة (GC وWS بالميجا).
- `measure mods` — قائمة موداتك بنسخها (تغني عن إرسال manifests).
- `measure reset` — تصفير العدادات لجولة جديدة.

يُحفظ نفس التقرير تلقائيًا في ملف `measure-report.json` داخل مجلد المود
(عند كتابة `measure` وعند العودة لشاشة العنوان).

## مثال على ما ستراه
```
Measure report (seconds since mod entry):
  mod-entry: 0s
  game-launched: 3.2s
  save-loaded: 12.5s
  day-started: 18.1s
  ticks: 1100, freezes>2s: 2 (total 5.3s), max gap: 3.1s
  mem launch: GC=210MB WS=340MB
```

## ما ترسله لي (سطر + ملف)
1. انسخ أسطر `Measure report` من السجل.
2. أرفق `measure-report.json` (ملف واحد).

## حد صريح
المود يرصد **الأعراض** (متى تجمّد وكم ذاكرة) **لا الجاني** — لا يمكن لمود قياس
زمن دخول مود آخر. تحديد المود البطيء يتم بجولة `measure_mods.py` (20/50/100).
