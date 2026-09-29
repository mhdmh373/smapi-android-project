# SMAPI Android Project — 4.3.2.5 (NRTnarathip) على Stardew Valley 1.6.15

## المخرجات
- `dist/SMAPI-Android-4_3_2_5-patched.zip` — زيب SMAPI المرقّع بالبنية المقبولة من اللانشر (deflate، بدون directory entries، ترتيب مطابق للأصلي).
- `dist/StardewModdingAPI.dll` / `dist/StardewModdingAPI.dll.orig` — المرقّع والأصلي بجانبه (قابل للعكس).
- `original/` — نسخ أصلية محفوظة (`StardewModdingAPI.dll.orig`, `BigInventory.dll.orig`, `Mono.Cecil.dll`).
- `docs/developer-mode-report.md` — تقرير وضع المطور (تحقق بنيوي فقط عبر Cecil، بدون تعديل DLL قبل الموافقة).
- `docs/limitations.md` — قائمة صريحة بما لم يُختبر.

## الأدوات
- `tools/patch/BinaryPatch.py` — ترقيع ثنائي مباشر (يحوّل 4 دوال إلى `ret`). لا يحتاج كتابة Cecil.
  ```
  pip install dnfile
  python3 tools/patch/BinaryPatch.py original/StardewModdingAPI.dll.orig dist/StardewModdingAPI.dll --keep-original dist/StardewModdingAPI.dll.orig
  ```
- `tools/patch/SmapiPatchCecil.cs` — مرجع بديل عبر Cecil.
- `scripts/build_zips.py` — بناء زيبات مقبولة من اللانشر + فحوصات آلية.

## بناء الزيب
```
python3 scripts/build_zips.py \
  --smapi-orig SMAPI-Android-4.3.2.5-(1775226918)-44436-4-3-2-5-1775227061.txt \
  --smapi-patched-dll dist/StardewModdingAPI.dll \
  --out-smapi dist/SMAPI-Android-4_3_2_5-patched.zip

python3 scripts/build_zips.py --verify dist/SMAPI-Android-4_3_2_5-patched.zip --orig-for-verify SMAPI-Android-4.3.2.5-(...).txt
```

## ما تم اختباره على الجهاز (سياق أصيل)
Scherm نظيفة + سجل يُكتب + قفل الشاشة لا يغلق اللعبة (أندرويد 13 / Launcher 1.1.7). اللانشر يرفض directory entries. تعديل `DeveloperMode=false` لم يُسكت الرسالة.

## ما لم يُختبر
انظر `docs/limitations.md` — كل تحقق عندنا بنيوي فقط.

## قواعد
- لا تخمين لأسماء `StardewValley.dll` ولا Harmony.
- لا منطق يتجاوز التحقق من نسخة اللعبة.
- كل تعديل على DLL قابل للعكس (الأصلي محفوظ بجانبه).
