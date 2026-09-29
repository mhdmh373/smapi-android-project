# SMAPI Android Project — 4.3.2.5 (NRTnarathip) على Stardew Valley 1.6.15

ترقيع SMAPI للأندرويد + مود تكبير الحقيبة، مع أدوات البناء والفحوصات الآلية.

> ⚠️ **كل المخرجات هنا تحقّق بنيوي فقط.** ما لم يُجرَّب على جهاز-android مكتوب صراحة في `docs/limitations.md`.

## المخرجات

| الملف | الوصف |
|---|---|
| `dist/SMAPI-Android-4_3_2_5-patched.zip` | زيب SMAPI مرقّع بالبنية المقبولة (38 ملف، deflate، بلا directory entries، ترتيب مطابق للأصلي) |
| `dist/StardewModdingAPI.dll` | الـ DLL المرقّع (4 دوال = `ret`) |
| `dist/StardewModdingAPI.dll.orig` | الأصلي بجانبه — كل تعديل قابل للعكس |
| `dist/BigInventory-v3.zip` | (مُسقط بطلب المستخدم 29/9 — للمرجع فقط) |
| `dist/SmartKeyboard-v1.1.zip` | مود زر الكيبورد العائم v1.0.1 (اختبار ذاتي self-test، بلا مرجع لعبة) — الرسم والتحميل مُجرَّبان على الجهاز |
| `original/` | نسخ أصلية محفوظة (`StardewModdingAPI.dll.orig`, `BigInventory.dll.orig`, `Mono.Cecil.dll`) |

## التقارير
- `docs/developer-mode-report.md` — من أين تأتي قيمة `DeveloperMode` ولماذا لا تتأثر بملفات الإعداد (تحليل Cecil، بدون تعديل DLL).
- `docs/biginventory-discovery.md` — اكتشاف-reflection الجديد لمرشحي `Items`/`MaxItems`.
- `docs/keyboard.md` — مود Smart Keyboard + قائمة اختباره (تحقق ساكن فقط).
- `docs/measure-guide.md` — دليل القياس م1 بخطوات الهاتف.
- `docs/limitations.md` — **قائمة صريحة بما لم يُختبر**.

## الأدوات

### ترقيع SMAPI
```bash
pip install dnfile
python3 tools/patch/BinaryPatch.py \
  original/StardewModdingAPI.dll.orig \
  dist/StardewModdingAPI.dll \
  --keep-original dist/StardewModdingAPI.dll.orig
```
يحوّل 4 دوال إلى `ret` واحد (تعديل ثنائي مباشر، بلا كتابة Cecil):
`StartLoggerToScreen`، `StopLoggerToScreen`، `OnLogImpl`، `SCore.<OnGameInitialized>b__62_0` (خيط الكونسول).
**لا يلمس `DeveloperMode`** — لإبقاء زر Share Log مفيداً.

### بناء الزيبات (المقبولة من اللانشر)
```bash
python3 scripts/build_zips.py \
  --smapi-orig "SMAPI-Android-4.3.2.5-(1775226918)-44436-4-3-2-5-1775227061.txt" \
  --smapi-patched-dll dist/StardewModdingAPI.dll \
  --out-smapi dist/SMAPI-Android-4_3_2_5-patched.zip

python3 scripts/build_zips.py --verify dist/SMAPI-Android-4_3_2_5-patched.zip \
  --orig-for-verify "SMAPI-Android-4.3.2.5-(1775226918)-44436-4-3-2-5-1775227061.txt"
```

### بناء BigInventory
```bash
bash scripts/build_biginventory.sh
python3 scripts/build_zips.py \
  --biginventory-dll dist/BigInventory/BigInventory.dll \
  --biginventory-manifest dist/BigInventory/manifest.json \
  --out-biginventory dist/BigInventory-v3.zip
```

### بناء SmartKeyboard + فحص بلا-مرجع-لعبة
```bash
bash scripts/build_keyboard.sh
python3 scripts/verify_nogameref.py src/SmartKeyboard dist/SmartKeyboard/SmartKeyboard.dll
python3 scripts/build_zips.py \
  --mod-name SmartKeyboard \
  --mod-dll dist/SmartKeyboard/SmartKeyboard.dll \
  --mod-manifest src/SmartKeyboard/manifest.json \
  --out-mod dist/SmartKeyboard-v1.zip
```

### أدوات القياس (م1، بايثون فقط)
```bash
python3 scripts/measure_boot.py SMAPI-latest.txt
python3 scripts/classify_crash.py SMAPI-latest.txt
python3 scripts/measure_mods.py --plan Mods
```

## الفحوصات الآلية (مطلوبة لكل zip)
1. لا directory entries
2. ترتيب الأسماء مطابق للأصلي (لزيب SMAPI)
3. `testzip()` بلا أخطاء
4. `manifest.json` صالح JSON وفيه `EntryDll` (للمودات)

## القيود الملزمة (مُطبَّقة)
- لا مرجع مباشر لـ `StardewValley.dll` ولا Harmony في أي كود من كتابة.
- لا تخمين لأسماء أعضاء اللعبة خارج قائمة المرشحين المصرّح بها.
- لا منطق يتجاوز التحقق من نسخة اللعبة ولا يدعم نسخاً مقرصنة.
- كل تعديل على `StardewModdingAPI.dll` قابل للعكس (الأصلي محفوظ).
- مود SmartKeyboard **مُنفّذ كمصدر نظيف** (لا نسخ AGPL) ويفترض توفر اللعبة وقت التشغيل فقط؛
  ملف `StardewValley.dll` للقراءة المحلية فقط ولا يُرفع للمستودع أبدًا.
- `DeveloperMode` **غير معدّل** في الـ DLL (موقوف حتى موافقتك).
