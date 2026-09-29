# قائمة صريحة بما لم يُختبر

> كل ما في هذا الملف هو **تحقق بنيوي فقط**. لم يُدّعَ أن أياً منه "يعمل" على جهاز.

## 1) ما هو مُوثّق كتجربة فعلية على جهاز (من سياقك أنت)
- الترقيع القديم (إفراغ `StartLoggerToScreen`/`StopLoggerToScreen`/`OnLogImpl` + خيط `RunConsoleInputLoop`): شاشة نظيفة، السجل يُكتب، قفل الشاشة لا يغلق اللعبة — أندرويد 13 / SMAPI Launcher 1.1.7 / Stardew Valley 1.6.15 / SMAPI 4.3.2.5.
- اللانشر يرفض أي زيب فيه directory entries أو ببنية/ترتيب مختلف.
- تعديل `smapi-internal/config.json` و `config.user.json` إلى `DeveloperMode=false` **لم يُسكت** رسالة `You enabled developer mode`.
- مود BigInventory (v2) يُحمَّل لكن يسجّل `couldn't find the player inventory`.

## 2) مخرجاتنا — بنيوية فقط، لم تُجرَّب على جهاز

### SMAPI
- `dist/SMAPI-Android-4_3_2_5-patched.zip`
  - `no directory entries` ✓ | ترتيب 38 ملف مطابق للأصلي ✓ | `testzip() is None` ✓
  - `smapi-internal/config.json` فيه `DeveloperMode: false` و `ListenForConsoleInput: false`
  - **لم يُثبَّت عبر اللانشر على جهاز حقيقي بعد.**
- `dist/StardewModdingAPI.dll` (مرقّع بـ `tools/patch/BinaryPatch.py`)
  - Cecil يؤكد أن الأربع دوال = 1 instruction (`ret`): ✓
  - **لم يُشغَّل داخل لعبة فعلية.**

### وضع المطور
- `docs/developer-mode-report.md` مبني على تفكيك ثنائي فقط (dnfile + Mono.Cecil + monodis).
- **لم يُختبر** ما إذا كان اللانشر يمرر `--developer-mode` أو يضبط `SMAPI_DEVELOPER_MODE`، أو يعيد كتابة `config.json`، أو أن المسار الفعلي على أندرويد يختلف.
- **لم يُعدَّل** أي DLL لوضع المطور (موقوف حتى موافقتك حفاظاً على زر Share Log).

### BigInventory
- `dist/BigInventory-v3.zip` — انظر `docs/biginventory-discovery.md`.
- **لم يُشغَّل على جهاز**: لا نعرف أي أسماء أعضاء في `Farmer` تطابق فعلاً في 1.6.15.
- فرضيتا `Items` و `MaxItems` **غير مؤكدتين**؛ المود يطبع حالتهما بدل الاعتماد عليهما.
- **لم يُختبر** التصغير/التكبير الفعلي ولا إعادة رسم واجهة الحقيبة.

## 3) موقوف / لم يُنفّذ
- مود لوحة المفاتيح/الأزرار الافتراضية — **لم يُنفّذ إطلاقاً**، بطلب صريح، وموقوف حتى تأكيد توفر `StardewValley.dll` أو جهاز مروّت.
- لم يُخترع أي اسم داخل `StardewValley.dll` ولم يُكتب كود Harmony عليه.
- لم يُضف أي منطق يتجاوز التحقق من نسخة اللعبة أو يتيح نسخاً مقرصنة.
- لم يُعدّل `DeveloperMode` في الـ DLL.
- لم يُنفّذ `git push` (يحتاج مصادقة GitHub من جهازك).

## 4) قيود بيئة البناء (مهمة لو أردت إعادة البناء)
حزمة `dotnet-sdk` في هذه البيئة ناقصة الأسماء (Alpine فقدت أسماء 1244 ملفاً). الحل المطبّق:
- استرجاع أسماء ملفات MSBuild (`.props`/`.targets`) من ترويسة كل ملف.
- استرجاع أسماء تجميعات Roslyn عبر قراءة جدول `Assembly` من كل PE.
- إضافة `csc.runtimeconfig.json` يدوياً وتشغيل `csc.dll` مباشرة.
- البناء يتم عبر `scripts/build_biginventory.sh` (لا يعتمد على `dotnet build`).

## 5) الفحوصات الآلية التي نجتازها محلياً
```
- zip: لا مدخلات مجلدات
- zip.testzip() is None
- SMAPI zip: ترتيب الأسماء مطابق للأصلي (38 ملف)
- manifest.json صالح JSON وفيه EntryDll
- Cecil: StartLoggerToScreen/StopLoggerToScreen/OnLogImpl/<OnGameInitialized>b__62_0 == 1 instr
- BigInventory.dll: مراجعه System.Runtime, System.Collections, StardewModdingAPI, System.Linq فقط (0 مرجع للعبة)
```
