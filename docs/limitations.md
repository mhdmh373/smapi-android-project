# قائمة صريحة بما لم يُختبر

> هذا القسم يوثّق كل ما هو **تحقق بنيوي فقط** ولم يُختبر على الجهاز الفعلي. لا تدّعي أن شيئاً “يعمل” خارج ما هو مذكور في السياق.

## ما اختُبر على الجهاز (سياق المُستخدم)
- الترقيع القديم (إفراغ `StartLoggerToScreen`/`StopLoggerToScreen`/`OnLogImpl` + خيط `RunConsoleInputLoop`) يحقق: شاشة نظيفة، السجل يُكتب، قفل الشاشة لا يغلق اللعبة — على أندرويد 13 + SMAPI Launcher 1.1.7 + Stardew Valley 1.6.15 + SMAPI 4.3.2.5.
- اللانشر يرفض أي زيب فيه directory entries أو ترتيب مختلف (مُختبر فعلياً).
- تعديل `smapi-internal/config.json` و `config.user.json` إلى `DeveloperMode=false` لم يُسكت رسالة `You enabled developer mode`.

## ما هو تحقق بنيوي فقط عندنا (لم يُختبر على جهاز)
- **الزيب المُعاد بناؤه الآن** (`dist/SMAPI-Android-4_3_2_5-patched.zip`): فحوصاته عندنا بنيوية فقط (no dir, order, testzip, manifest). لم يُثبّت عبر اللانشر على جهاز حقيقي بعد.
- **الـ DLL المرقّع الجديد** (`dist/StardewModdingAPI.dll` — BinaryPatch.py): تأكدنا أنه يفرغ الـ 4 دوال إلى `ret` واحد (Cecil: 1 instr لكل منها). لم يُشغّل داخل لعبة فعلية.
- **وضع المطور:** تقرير `docs/developer-mode-report.md` مبني على تفكيك `StardewModdingAPI.dll` (dnfile/Cecil/monodis) فقط. لم يُختبر ما إذا كان اللانشر يمرر `--developer-mode` أو يعيد كتابة `config.json`.
- **مود BigInventory:** متروك حالياً بطلب المستخدم — لم يُطبّق الإصلاح المقترح (اكتشاف Reflection + `Items`/`MaxItems`) ولم يُختبر.
- **مود لوحة المفاتيح/الأزرار الافتراضية:** لم يُنفّذ إطلاقاً بطلب صريح — موقوف حتى توفر `StardewValley.dll` أو جهاز مروّت.
- **التحقق من نسخة اللعبة:** لم يُضف أي منطق يتجاوز التحقق من نسخة اللعبة أو يتيح نسخاً مقرصنة (التزام بالقاعدة).

## ما لم يُنفّذ / مُعلق
- مود لوحة المفاتيح (ممنوع حتى إشعار).
- أي تعديل على `DeveloperMode` في الـ DLL (موقوف حتى موافقتك حفاظاً على زر Share Log).
- أي تخمين لأسماء داخل `StardewValley.dll` أو كود Harmony عليها (لا ملف اللعبة عندنا).

## الفحوصات البنيوية التي نجتازها محلياً
```
- zip: لا مدخلات مجلدات
- SMAPI zip: ترتيب الأسماء مطابق للأصلي (38 ملف)
- zip.testzip() is None
- manifest.json صالح وفيه EntryDll (للمودات)
- Cecil: StartLoggerToScreen/StopLoggerToScreen/OnLogImpl/<OnGameInitialized>b__62_0 == 1 instr (ret)
```
