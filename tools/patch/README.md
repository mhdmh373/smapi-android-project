# أداة ترقيع SMAPI للأندرويد — Tools/Patch

## ماذا تفعل
تفرغ 4 دوال فقط لتحقيق المطلوب على الجهاز:

- `StardewModdingAPI.Mobile.AndroidModLoaderManager.StartLoggerToScreen` → `ret`
- `StardewModdingAPI.Mobile.AndroidModLoaderManager.StopLoggerToScreen` → `ret`
- `StardewModdingAPI.Mobile.AndroidModLoaderManager.OnLogImpl` → `ret`
- `StardewModdingAPI.Framework.SCore.<OnGameInitialized>b__62_0` (خيط قراءة الكونسول) → `ret`

النتيجة الموثقة على جهاز أندرويد 13 + SMAPI Launcher 1.1.7: الشاشة نظيفة، السجل يُكتب للملف، قفل الشاشة لا يغلق اللعبة.

## ما لا تفعله
- لا تغيّر `DeveloperMode` (يبقى كما في `smapi-internal/config.json` + التراكبات `config.user.json` / `SMAPI-config.json` + متغير البيئة/وسائط التشغيل).
- زر **Share Log** يبقى معتمداً على السجل المفصّل، لذا لم نلمس `DeveloperMode` دون موافقتك.

## الأدوات
- `BinaryPatch.py` — الأداة الفعلية: تعديل ثنائي مباشر لآجسام الدوال (نحوّل كل واحدة إلى `tiny header 0x06 + ret 0x2A` ونصفّر الباقي). لا تحتاج كتابة Cecil، لذا لا تفشل حتى لو لم تتوفر تبعيات وقت التشغيل مثل `MonoGame.Framework.dll`.
  ```
  pip install dnfile
  python3 tools/patch/BinaryPatch.py input.dll output.dll --keep-original original.dll
  ```
- `SmapiPatchCecil.cs` — مرجع بديل عبر Mono.Cecil (قد يفشل كتابةً على بيئة تفتقد `MonoGame.Framework.dll`). يُبقي للتوثيق.

## الملف الأصلي
يُحفظ دائماً بجانب المرقّع كـ `original/StardewModdingAPI.dll.orig` (لا يُحذف ولا يُستبدل).
