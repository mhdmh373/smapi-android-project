# BigInventory — تقرير الحالة والاكتشاف

> **تنبيه:** كل التحقق هنا **بنيوي فقط**. لم يُشغَّل المود على جهاز أندرويد 13 ولا على Stardew Valley 1.6.15، ولا يوجد `StardewValley.dll` في هذه البيئة. الرسالة التي يطبعها المود **لم تُرَ بعد** على جهاز.

## المشكلة الأصلية
`BigInventory-v2` كان يسجّل:
```
Big Inventory: couldn't find the player inventory, skipping.
```
السبب: `GameAccess.GetInventory()` كان يجرّب اسمين فقط (`inventory` كخاصية/حقل) ولم يجدهما في 1.6.15، فيتوقف المود بلا أي معلومة تساعد على التشخيص.

## ما يفعله الإصدار 1.1.0

### 1) اكتشاف عبر Reflection (مطلوب المهمة)
`GameAccess.DumpFarmerInventoryMembers()` يطبع في السجل **كل أعضاء `Farmer`** الذين يطابقون أياً من الكلمات:
```
item, inventory, max, size, capacity, backpack
```
ويطبع لكل عضو: **النوع + القيمة الحالية** (و `canRead/canWrite` أو `static/initOnly` للخصائص/الحقول).

مثال على الشكل المتوقع في السجل (لم يُتحقق منه على جهاز):
```
Big Inventory diagnostics: Farmer type = StardewValley.Farmer
  PROPERTY Items : List<Item> | canRead=True canWrite=False | value = IEnumerable (enumerated 36, List`1[StardewValley.Item])
  FIELD maxItems : Int32 | static=False initOnly=False | value = 36
  METHOD getInventory() : returns StardewValley.Inventories.Inventory
Big Inventory diagnostics: matched 7 properties, 4 fields, 2 methods.
  CANDIDATE Farmer.inventory : PROPERTY of Inventory | canRead=True canWrite=False | value = ...
  CANDIDATE Farmer.Items : not found
  CANDIDATE Farmer.MaxItems : not found
```
`LogLevel.Debug` يعني أن الأسطر تظهر في السجل always (SMAPI يكتب DEBUG في الملف)، وعبارات الملخص `LogLevel.Info`.

### 2) تجربة المرشحين (فرضيات غير مؤكدة)
`ReportCandidates()` يطبع حالة كل مرشح صراحة: **PROPERTY / FIELD / METHOD / not found** مع النوع والقيمة.
المرشحون المطلوب تجربتهم: **`Items`** و **`MaxItems`**.

### 3) قاعدة "إذا لم يُعثر على شيء فلا تعدّل شيئاً"
في `ModEntry.ApplySize()`:
- إذا `GetCapacity()` أعاد `-1` (لا يوجد عضو سعة) → المود **لا يكتب أي قيمة**، يسجّل تحذيراً واحداً، ثم يطبع مرشحي السعة ويخرج.
- لا Harmony، لا تعديل على أي كائن آخر.

### 4) ترتيب البحث عن الحقيبة
1. `inventory` (خاصية/حقل)
2. مرشّحات: `Items`, `items`, `Backpack`, `backpack` (إن كانت `IList`)
3. بحث عام: أول عضو من نوع `IList` يحمل كلمة من كلمات الحقيبة

### 5) ترتيب البحث عن السعة
1. خاصية/حقل: `Capacity`, `capacity`, `Size`, `size`
2. مرشّحات: `MaxItems`, `maxItems`, `MaxSize`, `maxSize`, `InventorySize`, `inventorySize`
3. `IList.Count` كبديل للقراءة فقط
4. وإلا `-1` → **لا تعديل**

## أمر الكونسول الجديد
```
biginventory get          # يعرض الحجم الحالي ومصدر القراءة
biginventory set 48       # يضبط الحجم (12-120، مقرّب لأعلى مضاعف 12)
biginventory discover     # يعيد تشغيل الاكتشاف في أي وقت
```

## المخرجات
- `dist/BigInventory-v3.zip` (10,057 بايت) — ملفان فقط: `BigInventory/manifest.json` + `BigInventory/BigInventory.dll`
- المصدر: `src/BigInventory/{GameAccess.cs, ModConfig.cs, ModEntry.cs, BigInventory.csproj}`
- البناء: `scripts/build_biginventory.sh` (يستخدم `csc.dll` مباشرة لأن حزمة dotnet SDK في هذه البيئة ناقصة)

## فحوصات اجتازها الملف (بنيوية)
```
zip: لا مدخلات مجلدات ✓
zip.testzip() is None ✓
manifest.json صالح JSON وفيه EntryDll = "BigInventory.dll" ✓
مراجع التجميعات: System.Runtime, System.Collections, StardewModdingAPI, System.Linq فقط ✓
لا مرجع مباشر لـ StardewValley.dll ولا Harmony ✓ (تحقّق عددي: 0 ظهور)
الأنواع: BigInventory.GameAccess / ModConfig / ModEntry ✓
```

## ما لم يُختبر
- لم يُشغَّل المود على جهاز: لا نعرف أي أسماء أعضاء تطابق فعلاً في 1.6.15.
- **لا نعرف بعد** هل `Items` أو `MaxItems` هما الاسمان الصحيحان — لهذا المود يطبعهم صراحة بدل الاعتماد عليهما.
- لم يُختبر التصغير/التكبير فعلياً، ولا إعادة رسم واجهة الحقيبة.
- `Farmer` و `Game1` يظهران كنصوص في الكود فقط (أسماء Reflection)، وهما ليسا مرجعين مرتبطين.
