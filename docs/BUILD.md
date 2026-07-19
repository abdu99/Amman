# تعليمات البناء (Build Instructions)

## المتطلبات

- **Windows 10 (2004+) أو Windows 11** — كل من Aman.Encoder و Aman.Player تطبيقات WPF، ولا تعملان إلا على ويندوز.
- **.NET 8 SDK** (وليس فقط الـ Runtime) — [تنزيل](https://dotnet.microsoft.com/download/dotnet/8.0).
- PowerShell 5.1+ (مثبّت افتراضيًا على ويندوز) لتشغيل سكربتات `build/`.

> ملاحظة: هذا المشروع طُوّر ووُثّق داخل بيئة Linux بدون .NET SDK متاح، لذا **لم يُبنَ أو يُختبر فعليًا هنا**. الكود مكتوب ليُبنى ويعمل على ويندوز حسب الخطوات التالية — إن واجهت أي خطأ ترجمة (compile error) عند البناء الفعلي فهو أمر متوقع لأي مشروع بهذا الحجم لم يُختبر بعد؛ راجع رسالة الخطأ وعدّل الملف المشار إليه.

## البنية

```
Aman/
├── src/
│   ├── Aman.Shared/     مكتبة مشتركة: التشفير، صيغة الحاوية، نظام التراخيص
│   ├── Aman.Encoder/    تطبيق الإدارة (يُنتج حزم Player.exe)
│   └── Aman.Player/     المشغّل الناتج (يُبنى أولًا كـ "stub" يُلحق به Encoder بيانات مشفّرة)
├── build/               سكربتات PowerShell للبناء والنشر
└── docs/                هذا التوثيق
```

## لماذا يجب بناء Player أولًا؟

فكرة "تصدير كملف EXE واحد" تُنفَّذ بأسلوب **stub + appended data**:
Aman.Encoder لا يُترجم Aman.Player داخليًا، بل يقرأ نسخة **مبنية مسبقًا** من `Player.exe` من المجلد
`src/Aman.Encoder/Resources/PlayerStub/Player.exe`، وعند "تصدير الحزمة" ينسخ بايتات هذا الملف
كما هي ثم **يُلحق بها** بيانات الفيديو المشفّرة + رأس الحاوية + بصمة نهاية الملف (trailer).
لذلك：`Player.exe` نفسه يجب أن يكون منشورًا كملف EXE واحد (self-contained, single-file) **قبل** بناء Encoder.

## البناء بأمر واحد

```powershell
cd Aman
build\build-all.ps1
```

هذا يُنفّذ `publish-player.ps1` ثم `publish-encoder.ps1` بالترتيب الصحيح.

## البناء خطوة بخطوة

```powershell
cd Aman

# 1) ابنِ المشغّل أولًا وانسخه إلى مجلد الـ stub داخل Encoder
build\publish-player.ps1

# 2) الآن ابنِ الـ Encoder (يتضمن الـ stub الذي بنيناه للتو)
build\publish-encoder.ps1
```

الناتج النهائي:
```
src/Aman.Encoder/bin/Release/net8.0-windows/win-x64/publish/Aman.Encoder.exe
```

هذا هو الملف الذي تُشغّله لإدارة الحزم. كل حزمة تُصدّرها منه ستكون ملف `.exe` واحد جاهز للتوزيع.

## البناء من Visual Studio (بديل)

يمكنك أيضًا فتح `Aman.sln` في Visual Studio 2022 (17.8+) وبناء المشاريع يدويًا، لكن يجب اتباع
نفس الترتيب: انشر (Publish) مشروع `Aman.Player` أولًا بإعدادات:
- Deployment mode: **Self-contained**
- Target runtime: **win-x64**
- ✅ Produce single file
- ✅ Enable ReadyToRun compilation: **لا** (اتركها معطّلة لتقليل الحجم/التعقيد)

ثم انسخ الناتج (`Player.exe`) يدويًا إلى `src/Aman.Encoder/Resources/PlayerStub/Player.exe`،
وبعدها انشر مشروع `Aman.Encoder` بنفس الإعدادات.

## الأيقونة والعلامة التجارية

المشروع لا يتضمن أيقونة `.ico` جاهزة. لإضافة أيقونتك:
1. ضع ملف `aman.ico` داخل `src/Aman.Encoder/Resources/` و `src/Aman.Player/Resources/`.
2. فعّل السطر المُعلَّق `<ApplicationIcon>Resources\aman.ico</ApplicationIcon>` في كل من
   `Aman.Encoder.csproj` و `Aman.Player.csproj`.

شعار المشغّل نفسه (الذي يظهر داخل نافذة فك القفل) **لا يحتاج بناءً من جديد** — يُختار من واجهة
Aman.Encoder (PNG) ويُحفظ داخل كل حزمة عند التصدير، فيمكن لكل حزمة أن يكون لها شعار مختلف
دون إعادة بناء أي شيء.

## ملاحظة حول التطوير والتجربة

`Aman.Player` كما هو مكتوب يقرأ دائمًا الحاوية المُلحقة **بملفه التنفيذي الخاص هو** (عبر
`SelfExeLocator.GetCurrentExecutablePath()`), أي أن أسرع طريقة لتجربة تعديل على منطق المشغّل هي:
عدّل الكود → نفّذ `build\publish-player.ps1` (يبني وينسخ الـ stub) → `build\publish-encoder.ps1` →
صدّر حزمة تجريبية صغيرة (فيديو قصير) من واجهة Encoder وشغّلها. لا توجد اليوم آلية لتحميل حاوية
خارجية بمعزل عن هذا المسار؛ إن احتجت تدفّق تطوير أسرع، أضف علمًا اختياريًا
(مثل: قراءة مسار الحاوية من متغيّر بيئة عند التصحيح فقط) بدلاً من الاعتماد على الملف الذاتي.
