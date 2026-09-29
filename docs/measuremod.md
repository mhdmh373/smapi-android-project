# Measure — مود القياس على الجهاز

> **تحقق ساكن فقط** حتى تجربه. يستخدم واجهات SMAPI العامة فقط.

## التثبيت
Mod Manager ← Install Mod ← `MeasureMod-v1.1.zip`. لا إعدادات ولا كونسول.

## كيف تقرأ النتيجة (بدون كونسول)
المود يكتب تقريره **تلقائيًا في ملف السجل** (`SMAPI-*.txt`):
- عند بداية كل يوم داخل اللعبة.
- عند العودة لشاشة العنوان.
- كل 5 دقائق لعب.
ابحث في السجل عن `Measure report`. ويُحفظ نفس التقرير في
`Mods/Measure/measure-report.json` (أرفقه كما هو).

من يملك كونسول (محاكي/كمبيوتر): `measure` و`measure mods` و`measure reset` ما زالت تعمل.

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
