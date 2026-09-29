# تقرير وضع المطور — StardewModdingAPI.dll (SMAPI 4.3.2.5 للأندرويد)

> **تنبيه منهجي:** كل ما يلي هو **تحقق بنيوي فقط** عبر تفكيك `StardewModdingAPI.dll` (dnfile + Mono.Cecil + monodis). لم يُختبر على جهاز أندرويد 13 / SMAPI Launcher 1.1.7 أو على اللعبة 1.6.15. لم يُعدّل أي DLL لهذا التقرير (التعديل موقوف حتى موافقتك لأن زر **Share Log** قد يعتمد على السجل المفصّل).

## 1) من أين تأتي قيمة `DeveloperMode`؟

### 1.1 التعريف
- `SConfig.DeveloperMode : bool` — الخاصية الوحيدة التي تحدد “وضع المطور” (ظهور رسائل TRACE في الكونسول + رسالة `You enabled developer mode...`).
- الحقل الخلفي: `<DeveloperMode>k__BackingField` (Field 999).
- الدالة الوحيدة التي تكتبها مباشرة: `SConfig.OverrideDeveloperMode(bool value)` → `set_DeveloperMode(value)`.

### 1.2 مصادر القيمة (ترتيب التطبيق الفعلي)

**أولاً: `Program.Start(string[] args)` (RVA 18300)**

```csharp
bool writeToConsole = !args.Contains("--no-terminal") && Env("SMAPI_NO_TERMINAL")==null;
string rawModsPath = null;
Nullable<bool> developerMode = null;

int pathIndex = Array.LastIndexOf(args, "--mods-path")+1;
if (pathIndex>=1 && args.Length>=pathIndex) rawModsPath = args[pathIndex];

if (args.Contains("--developer-mode")) developerMode = true;
if (args.Contains("--developer-mode-off")) developerMode = false;

if (IsNullOrWhiteSpace(rawModsPath)) rawModsPath = Env("SMAPI_MODS_PATH");
if (!developerMode.HasValue) {
    string raw = Env("SMAPI_DEVELOPER_MODE");
    if (raw != null) developerMode = bool.Parse(raw);
}
modsPath = IsNullOrWhiteSpace(rawModsPath) ? Constants.DefaultModsPath : Path.Combine(Constants.GamePath, rawModsPath);

new SCore(modsPath, writeToConsole, developerMode).RunInteractively();
```

> أي `developerMode` تمرّ إما كوسيط سطر أوامر أو متغير بيئة **قبل** قراءة أي `config.json`. القيمة `null` تعني “استخدم ما في الملفات”.

**ثانياً: `SCore..ctor(string modsPath, bool writeToConsole, bool? overrideDeveloperMode)` (RVA 72344)**

```csharp
this.OverrideDeveloperMode = overrideDeveloperMode;
string logPath = GetLogPath();
var deserializer = new JsonSerializerSettings{ NullValueHandling=Ignore };

SConfig settings = JsonConvert.DeserializeObject<SConfig>(File.ReadAllText(Constants.ApiConfigPath));
// Constants.ApiConfigPath = <GamePath>/smapi-internal/config.json

if (File.Exists(Constants.ApiUserConfigPath))  // smapi-internal/config.user.json
    JsonConvert.PopulateObject(File.ReadAllText(...), settings, deserializer);
if (File.Exists(Constants.ApiModGroupConfigPath)) // <ModsPath>/SMAPI-config.json
    JsonConvert.PopulateObject(File.ReadAllText(...), settings, deserializer);

if (this.OverrideDeveloperMode.HasValue)
    settings.OverrideDeveloperMode(this.OverrideDeveloperMode.Value);

this.Settings = settings;
this.LogManager = new LogManager(logPath, settings.ConsoleColorScheme, settings.ConsoleColorSchemes,
                                 writeToConsole, settings.VerboseLogging,
                                 isDeveloperMode: settings.DeveloperMode, ...);
```

**الخلاصة:** `DeveloperMode` النهائي = قيمة `config.json` **يُطبّق فوقها** `config.user.json` ثم `SMAPI-config.json` ثم أخيراً **وسيط/بيئة** `Program.Start` إن وُجدت. لا يوجد فرض قسري في `SCore` نفسه.

### 1.3 أين يُستهلك؟

- `LogManager` و `Monitor` يستقبلان `isDeveloperMode` في البناء ويُظهرون `ShowTraceInConsole` و `You enabled developer mode...` عند `LogIntro`.
- `GetCustomSettings()` تُرجع فقط الإعدادات المختلفة عن الافتراضي، وتُطبع كـ `Loaded with custom settings: `.

## 2) لماذا لم تتغير عند تعديل `smapi-internal/config.json` و `config.user.json`؟

بناءً على التفكيك، **الكود يحترم الملفات** — لذا أحد هذه الاحتمالات يفسّر بقاء الرسالة:

1. **اللانشر يزوّد الوسيط/المتغير:** SMAPI Launcher 1.1.7 قد يستدعي `StardewModdingAPI.dll` مع `--developer-mode` أو يضبط `SMAPI_DEVELOPER_MODE=true`. الوسيط يسبق ويتجاوز أي `config.json` (كما رأينا: `OverrideDeveloperMode` يُطبّق بعد قراءة الملفات).
2. **الملفات تُكتب من جديد:** اللانشر أو سكربت التثبيت قد يعيد كتابة `smapi-internal/config.json` عند كل تشغيل إلى القيمة الافتراضية `DeveloperMode: true` (الملف المرفق في الزيب الأصلي هو `true`).
3. **المسار غير المتوقع على أندرويد:** على أندرويد يستخدم SMAPI `EarlyConstants.ExternalFilesDir` و `ModsPath` مختلف (قد يكون `/storage/emulated/0/...` أو مجلد خاص بالـ app). الملف الذي عدّلته قد لا يكون هو `Constants.ApiConfigPath` الفعلي على الجهاز.
4. **الكاش أو السجل القديم:** رسالة `You enabled developer mode` قد تكون من سجل سابق لم يُطهّر (`PurgeNormalLogs`).

> خيار مستبعد: نسخة SMAPI المرقّعة الحالية (`SMAPI-Android-4_3_2_5-patched-v2.zip`) **لا تفرض** `DeveloperMode=true` في الكود — `Program.Start` و `SCore..ctor` متطابقان مع الأصلي حتى البايت (123 instruction، لا `ldc.i4.1` إضافي). الفارق الوحيد المثبت هو إفراغ 4 دوال للسجل/الكونسول.

## 3) ماذا يقول ثنائي الزيب المرقّع الحالي؟

- الحجم 1,095,168 مقابل 1,243,136 أصلي — السبب إعادة كتابة Cecil ضغطت الـ PE، لكن الأهم:
- **4 دوال صارت `ret` وحيد (tiny 0x06 0x2A):** `StartLoggerToScreen` / `StopLoggerToScreen` / `OnLogImpl` / `SCore.<OnGameInitialized>b__62_0`. هذا وحده يحقق “الشاشة نظيفة والسجل يُكتب”.
- `config.json` داخل الزيب المرقّع مضبوط `DeveloperMode: false` لكنّه يُقرأ عند التشغيل — إذا كان اللانشر يزوّد الوسيط، فالقيمة ستُعاد إلى `true`.

## 4) توصية (بدون تعديل DLL حتى موافقتك)

1. **افحص اللانشر أولاً:** ابحث في إعدادات/ملفات SMAPI Launcher عن خيار `Developer Mode` أو معطيات التشغيل. جرّب تشغيل اللعبة مع `SMAPI_DEVELOPER_MODE=false` في البيئة أو أضف `--developer-mode-off` لوسائط التشغيل إن كان اللانشر يسمح.
2. **تأكّد من المسار على الجهاز:** شغّل `adb shell ls -R` أو استخدم مدير ملفات لمعرفة المكان الفعلي لـ `smapi-internal/config.json` الذي يقرأه SMAPI (قد يكون تحت `Android/data/...`).
3. **إن لزم تعديل DLL:** الحل سيكون إزالة تزويد اللانشر للوسيط، لا فرض `DeveloperMode=false` في الكود — إلا إذا أردت ترقيعاً يعكس `OverrideDeveloperMode` (أي يجبر `false` حتى مع الوسيط). هذا يُنصح به فقط بعد موافقتك لأنه قد يجعل سجلات `Share Log` أقل تفصيلاً عند الحاجة للدعم.

## 5) كيف أُنتج هذا التقرير؟

- `dnfile` لقراءة PE/Metadata (`MethodDef`, `TypeDef`, `Field`, `#US`, `#Strings`).
- `Mono.Cecil` (0.11.6) لتفكيك IL (`SCore..ctor`, `Program.Start`, `SConfig.*`).
- `monodis` للتحقق من `RVA`/`MaxStack`.
- المقارنة الثنائية `orig vs patched` أكدت أن 4 دوال فقط تغيّرت (ورسالة `You enabled developer mode` ما زالت `utf16le` عند 712750، بأربع دوال لا بالكود المفروض).

## 6) الملحق — آثار لم تُختبر

- لم يُختبر سلوك `DeveloperMode` على جهاز حقيقي (لا StardewValley.dll عندي).
- لم يُختبر أثر تعديل `SMAPI-config.json` في مجلد `Mods/` على أندرويد.
- لم يُختبر ما إذا كان اللانشر يُعيد كتابة `config.json` عند التثبيت.

