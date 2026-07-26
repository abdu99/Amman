# تعليمات البناء (Build Instructions)

## أسرع طريقة: GitHub Actions (بدون الحاجة لجهاز Windows)

كل push يمسّ هذا المجلد (`downloader/**`) يُشغّل `.github/workflows/build-downloader.yml` تلقائيًا
على خادم Windows سحابي، وينتج `MultiDownloader.exe` كملف تحميل (artifact) جاهز — تجده في تبويب
**Actions** في GitHub. لا يتطلب أي إعداد محلي.

## المتطلبات (للبناء المحلي)

- **Windows 10 (2004+) أو Windows 11** — التطبيق WPF، ولا يعمل إلا على ويندوز.
- **.NET 8 SDK** — [تنزيل](https://dotnet.microsoft.com/download/dotnet/8.0).
- PowerShell 5.1+ (مثبّت افتراضيًا على ويندوز).

> ملاحظة: هذا المشروع طُوّر ووُثّق داخل بيئة Linux بدون .NET SDK متاح، لذا **لم يُبنَ أو يُختبر
> فعليًا هنا**. إن واجهت خطأ ترجمة (compile error) عند البناء الفعلي فراجع رسالة الخطأ وعدّل
> الملف المشار إليه — متوقع لأي مشروع بهذا الحجم لم يُختبر بعد.

## البناء بأمر واحد

```powershell
cd downloader
build\build-all.ps1
```

الناتج: `src/MultiDownloader.App/bin/Release/net8.0-windows/win-x64/publish/MultiDownloader.exe`
(ملف EXE واحد ذاتي الاكتفاء self-contained، لا يحتاج تثبيت .NET Runtime على جهاز المستخدم النهائي).

## yt-dlp و ffmpeg

- **yt-dlp**: لا يُوزَّع مع هذا المستودع. عند أول تشغيل، إن لم يجده البرنامج (بجانب الـ exe، أو في
  `PATH`، أو في `%AppData%\MultiDownloader\tools`) فسيُنزِّله تلقائيًا من
  [أحدث إصدار رسمي](https://github.com/yt-dlp/yt-dlp/releases/latest) عبر HTTPS. لتضمينه يدويًا
  بدل الاعتماد على التنزيل التلقائي، ضع `yt-dlp.exe` في مجلد `tools\` بجانب `MultiDownloader.exe`
  بعد النشر.
- **ffmpeg**: مطلوب فقط عند اختيار جودة تحتاج دمج تيار فيديو وصوت منفصلين (كخيارات "أفضل جودة"
  على يوتيوب غالبًا)، أو عند استخراج MP3. **لا يُنزَّل تلقائيًا** — ثبّته يدويًا وتأكد أنه في
  `PATH`، أو ضع `ffmpeg.exe` في `tools\` بجانب البرنامج، أو حدّد مساره من نافذة الإعدادات داخل
  البرنامج. بدونه، تظل خيارات الجودة المُدمَجة مسبقًا (progressive formats) تعمل بلا مشاكل.

## البناء خطوة بخطوة (بدون سكربت)

```powershell
cd downloader
dotnet publish src/MultiDownloader.App/MultiDownloader.App.csproj -c Release -r win-x64 `
    --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```
