# تشخيص خطأ SVE — السبب الجذري مؤكد ساكنًا

> الأدلة: سجل جهاز المستخدم (16:53:14، SVE 1.15.11) + `tools/mdparse.py`
> على `StardewValley.dll` version 1.6.15.24354 من نسخته.

## 1) ما يقوله السجل [مُجرَّب على الجهاز]
```
ERROR Stardew Valley Expanded — Mod crashed on entry
NullReferenceException: Null method for FlashShifter.SVECode
  at HarmonyLib.Harmony.Patch(MethodBase original, ...)
  at StardewModdingAPI.Mobile.Mods.SveFix.TMXLLoadMapFacingDirection_ApplyPatch(Harmony, IMonitor)
  at ...StardewValleyExpanded.HarmonyPatch_TMXLLoadMapFacingDirection.ApplyPatch_Patch1(...)
  at StardewValleyExpanded.ModEntry.Entry(...)
```
- اللعبة **تكمل الإقلاع** بعده (`Mods loaded and ready!`) — SVE يعمل ناقصًا (بلا إصلاح اتجاه الالتفاف) لا انهيار كامل.
- البادئة `ApplyPatch_Patch1` تعني: نوع `HarmonyPatch_TMXLLoadMapFacingDirection` **موجود** في SVE 1.15.11
  (البحث `GetType` نجح)، والانفجار **داخل** بادئة SveFix عند `harmony.Patch(original: null, ...)`.
- `Smart Keyboard` يُحمَّل بلا أخطاء في نفس السجل (فقط ملاحظة DEBUG: بلا مفاتيح تحديث).

## 2) التوقيع الحقيقي في اللعبة [تحقق ساكن فقط]
`Game1.warpFarmer` في 1.6.15.24354 له 4 نسخ، ومنها:
```
warpFarmer(StardewValley.LocationRequest, int, int, int, bool) -> void   // 5 معاملات
```
ولا توجد نسخة `(LocationRequest, int, int, int)` (4 معاملات).

## 3) السبب الجذري
كود `SveFix` الحالي (HEAD في SMAPI-Android-1.6) يبحث عن النسخة رباعية المعاملات:
```csharp
AccessTools.Method(typeof(Game1), "warpFarmer",
    [typeof(LocationRequest), typeof(int), typeof(int), typeof(int)])
```
فيعيد `null` → `harmony.Patch(original: null)` → `NullReferenceException: Null method...`.
وهذا **تراجع (regression)** من commit `f7656bb` («Fixed: SVE patch fix method warpFarmer broken code»)
الذي غيّر البحث من 5 معاملات (مع `bool doFade`) إلى 4. النسخة الخماسية كانت الصحيحة للعبة 1.6.15.

## 4) الإصلاح المقترح (مصدر SMAPI-Android-1.6 — يحتاج بناءً غير متاح هنا)
```csharp
// 1) أعد المعامل الخامس:
original: AccessTools.Method(typeof(Game1), "warpFarmer",
    [typeof(LocationRequest), typeof(int), typeof(int), typeof(int), typeof(bool)]),
// 2) أعد doFade للبادئة:
static void Sve_TXMLMapFacingDir_warpFarmer(LocationRequest locationRequest, int tileX, int tileY,
    ref int facingDirectionAfterWarp, bool doFade)
// 3) احرس كل شيء (لا تُسقط دخول المود أبدًا):
if (TMXLLoadMapFacingDirection == null) { monitor.Log("SveFix: SVE patch type not found, skipping.", LogLevel.Warn); return true; }
MethodBase target = AccessTools.Method(...);
if (target == null) { monitor.Log("SveFix: Game1.warpFarmer overload not found, skipping.", LogLevel.Warn); return true; }
try { harmony.Patch(target, prefix: ...); } catch (Exception ex) { monitor.Log($"SveFix: patch failed, skipping. {ex.GetType().Name}", LogLevel.Warn); }
return true; // دع ApplyPatch الأصلية تعمل دائمًا
```
- `return true` (لا false) عند الفشل: البادئة Harmony تعني false = تخطّي الأصل — لا نريد كسر SVE أكثر.
- يُقترح كـ patch موثق على مستودع 1.6 (أو JunimoGate-SMAPI الذي أزال إصلاحات المودات أصلًا).

## 5) بدائل المستخدم الآن (بدون بناء)
- إزالة SVE مؤقتًا للتأكد أن بقية المودات تدخل اللعبة (تشخيص، ليس حلًا).
- تجربة SVE أقدم؟ **لا يُنصح** — قد يكسر الحفظ. الأفضل انتظار الإصلاح المصدري.

## 6) نتيجة جانبية: BigInventory على 1.6.15 [تحقق ساكن فقط]
- `Farmer` يحوي: FIELD `maxItems` + PROPERTY `MaxItems` (قراءة/كتابة) + FIELD `netItems`
  و`get_MaxItems() -> int` مؤكد، و`get_Items() -> StardewValley.Inventories.Inventory`
  (موروثة على الأرجح — لهذا لم تظهر في مسح الأعضاء المباشر).
- مود v3 يجرّب `maxItems/MaxItems` صراحة ويبحث عامًا بكلمة `item` (تلتقط `netItems`
  إن كانت `IList`) — **قد يعمل already؛ بانتظار سطر diagnostics من جهازه**.
- لا تغيير كود قبل رؤية diagnostics (القاعدة: لا تحسّن ما لم تقِسه).
