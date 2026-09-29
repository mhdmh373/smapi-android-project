# قائمة صريحة بما لم يُختبر

> كل ما في هذا الملف هو **تحقق بنيوي فقط**. لم يُدّعَ أن أياً منه "يعمل" على جهاز.

## 1) ما هو مُوثّق كتجربة فعلية على جهاز (من سياقك أنت)
- الترقيع القديم (إفراغ `StartLoggerToScreen`/`StopLoggerToScreen`/`OnLogImpl` + خيط `RunConsoleInputLoop`): شاشة نظيفة، السجل يُكتب، قفل الشاشة لا يغلق اللعبة — أندرويد 13 / SMAPI Launcher 1.1.7 / Stardew Valley 1.6.15 / SMAPI 4.3.2.5.
- اللانشر يرفض أي زيب فيه directory entries أو ببنية/ترتيب مختلف.
- تعديل `smapi-internal/config.json` و `config.user.json` إلى `DeveloperMode=false` **لم يُسكت** رسالة `You enabled developer mode`.
- مود BigInventory (v2) يُحمَّل لكن يسجّل `couldn't find the player inventory`.

## 2) مخرجاتنا — بنيوية فقط، لم تُجرَّب على جهاز

### أجهزة موثقة من المستخدم (تحديث 29/9)
- الهاتف: Samsung M52، رام 8GB — يحدد سقف الـ100 مود (النتيجة من القياس النهائي).
- اللعبة: 1.6.15.0 build 24354 — طابقت `StardewValley.dll` المُستلم (سلسلة `1.6.15.24354` بداخله) [تحقق ساكن فقط].
- موضع زر الكيبورد: يمين الشاشة تحت (!) و(≡) — من `docs/keyboard-placement.jpg`.
- `StardewValley.dll` يُستخدم للقراءة فقط خارج المستودع (`game-ref-local-only/`) ولا يُرفع أبدًا — وجوده حاليًا في تاريخ GitHub العام **يجب حذفه** (انظر قسم 6).

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
- مود لوحة المفاتيح: **نُفّذ الآن كمصدر نظيف** (`src/SmartKeyboard`، زيب `dist/SmartKeyboard-v1.zip`)
  بنفس شروط البنية (ملفان، بلا مجلدات، testzip سليم، EntryDll) وبلا مرجع لعبة
  (فحص `verify_nogameref.py`) — **لكنه لم يُشغَّل على جهاز بعد**.
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
- SmartKeyboard.dll: بلا أي اسم تجميع ASCII للعبة (MonoGame/Harmony/xTile/StardewValley) + الأسماء الافتراضية الستة موجودة في تعداد SButton
```

## 6) تحذير: ملف اللعبة في تاريخ GitHub العام
- رُفع `StardewValley.dll` (9MB) إلى المستودع العام عبر الويب — مخالف لقاعدة
  «للقراءة فقط ولا يُرفع». النسخة المحلية الآن خارج التتبع (`game-ref-local-only/`).
- المطلوب من الهاتف (3 نقرات): افتح الملف في GitHub ← ⋮ ← Delete ← Commit.
  التاريخ سيبقى يحويه حتى إعادة كتابة التاريخ — أخبرني لأجهز لك أمرًا واحدًا
  لتنظيفه إن أردت (يحتاج توكن لمرة واحدة، أطلبه أدناه).
