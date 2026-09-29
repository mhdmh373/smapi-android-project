# SMAPI Android Project — 4.3.2.5 (NRTnarathip) على Stardew Valley 1.6.15

ترقيع SMAPI للأندرويد + مود تكبير الحقيبة، مع أدوات البناء والفحوصات الآلية.

> ⚠️ **كل المخرجات هنا تحقّق بنيوي فقط.** ما لم يُجرَّب على جهاز-android مكتوب صراحة في `docs/limitations.md`.

## المخرجات

| الملف | الوصف |
|---|---|
| `dist/SMAPI-Android-4_3_2_5-patched.zip` | زيب SMAPI مرقّع بالبنية المقبولة (38 ملف، deflate، بلا directory entries، ترتيب مطابق للأصلي) |
| `dist/StardewModdingAPI.dll` | الـ DLL المرقّع (4 دوال = `ret`) |
| `dist/StardewModdingAPI.dll.orig` | الأصلي بجانبه — كل تعديل قابل للعكس |
| `dist/BigInventory-v3.zip` | مود تكبير الحقيبة مع اكتشاف Reflection |
| `original/` | نسخ أصلية محفوظة (`StardewModdingAPI.dll.orig`, `BigInventory.dll.orig`, `Mono.Cecil.dll`) |

## التقارير
- `docs/developer-mode-report.md` — من أين تأتي قيمة `DeveloperMode` ولماذا لا تتأثر بملفات الإعداد (تحليل Cecil، بدون تعديل DLL).
- `docs/biginventory-discovery.md` — اكتشاف-reflection الجديد لمرشحي `Items`/`MaxItems`.
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
- مود لوحة المفاتيح/الأزرار الافتراضية **موقوف** حتى تأكيد توفر `StardewValley.dll` أو جهاز مروّت.
- `DeveloperMode` **غير معدّل** في الـ DLL (موقوف حتى موافقتك).
