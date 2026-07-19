namespace Aman.Shared.Localization;

internal static class Strings
{
    public static readonly Dictionary<string, Dictionary<AppLanguage, string>> Table = new()
    {
        // Shared / app chrome
        ["app_name"] = new() { [AppLanguage.Arabic] = "أمان", [AppLanguage.English] = "AMAN" },
        ["language_toggle"] = new() { [AppLanguage.Arabic] = "English", [AppLanguage.English] = "العربية" },
        ["theme_toggle"] = new() { [AppLanguage.Arabic] = "الوضع الليلي/النهاري", [AppLanguage.English] = "Dark/Light Mode" },

        // Encoder
        ["encoder_title"] = new() { [AppLanguage.Arabic] = "أمان — إدارة الحزم", [AppLanguage.English] = "AMAN — Package Manager" },
        ["add_videos"] = new() { [AppLanguage.Arabic] = "إضافة فيديوهات", [AppLanguage.English] = "Add Videos" },
        ["drop_videos_hint"] = new() { [AppLanguage.Arabic] = "اسحب وأفلت ملفات الفيديو هنا، أو اضغط للتصفح", [AppLanguage.English] = "Drag & drop video files here, or click to browse" },
        ["playlist"] = new() { [AppLanguage.Arabic] = "قائمة التشغيل", [AppLanguage.English] = "Playlist" },
        ["remove"] = new() { [AppLanguage.Arabic] = "إزالة", [AppLanguage.English] = "Remove" },
        ["protection_settings"] = new() { [AppLanguage.Arabic] = "إعدادات الحماية", [AppLanguage.English] = "Protection Settings" },
        ["password"] = new() { [AppLanguage.Arabic] = "كلمة السر", [AppLanguage.English] = "Password" },
        ["confirm_password"] = new() { [AppLanguage.Arabic] = "تأكيد كلمة السر", [AppLanguage.English] = "Confirm Password" },
        ["protection_mode"] = new() { [AppLanguage.Arabic] = "نوع الحماية", [AppLanguage.English] = "Protection Mode" },
        ["mode_password_only"] = new() { [AppLanguage.Arabic] = "كلمة سر عامة", [AppLanguage.English] = "Shared Password" },
        ["mode_activation_code"] = new() { [AppLanguage.Arabic] = "كود تفعيل مرتبط بالجهاز", [AppLanguage.English] = "Device-Locked Activation Code" },
        ["expiry_enabled"] = new() { [AppLanguage.Arabic] = "تحديد تاريخ انتهاء", [AppLanguage.English] = "Set Expiry Date" },
        ["max_runs_enabled"] = new() { [AppLanguage.Arabic] = "تحديد عدد مرات التشغيل", [AppLanguage.English] = "Limit Number of Runs" },
        ["branding"] = new() { [AppLanguage.Arabic] = "العلامة التجارية", [AppLanguage.English] = "Branding" },
        ["player_title"] = new() { [AppLanguage.Arabic] = "عنوان المشغّل", [AppLanguage.English] = "Player Title" },
        ["logo"] = new() { [AppLanguage.Arabic] = "الشعار", [AppLanguage.English] = "Logo" },
        ["accent_color"] = new() { [AppLanguage.Arabic] = "اللون المميز", [AppLanguage.English] = "Accent Color" },
        ["build_package"] = new() { [AppLanguage.Arabic] = "تصدير الحزمة كملف EXE", [AppLanguage.English] = "Export Package as EXE" },
        ["building"] = new() { [AppLanguage.Arabic] = "جارٍ البناء…", [AppLanguage.English] = "Building…" },
        ["build_success"] = new() { [AppLanguage.Arabic] = "تم تصدير الحزمة بنجاح", [AppLanguage.English] = "Package exported successfully" },
        ["vendor_identity"] = new() { [AppLanguage.Arabic] = "هوية التوقيع (البائع)", [AppLanguage.English] = "Vendor Signing Identity" },
        ["generate_activation_code"] = new() { [AppLanguage.Arabic] = "توليد كود تفعيل لجهاز", [AppLanguage.English] = "Generate Activation Code" },
        ["machine_id_input"] = new() { [AppLanguage.Arabic] = "معرّف جهاز المستخدم", [AppLanguage.English] = "Customer Machine ID" },

        // Player
        ["unlock_title"] = new() { [AppLanguage.Arabic] = "أدخل كلمة السر للمتابعة", [AppLanguage.English] = "Enter password to continue" },
        ["unlock_activation_title"] = new() { [AppLanguage.Arabic] = "أدخل كود التفعيل", [AppLanguage.English] = "Enter activation code" },
        ["unlock_button"] = new() { [AppLanguage.Arabic] = "فتح", [AppLanguage.English] = "Unlock" },
        ["wrong_password"] = new() { [AppLanguage.Arabic] = "كلمة السر غير صحيحة", [AppLanguage.English] = "Incorrect password" },
        ["invalid_activation_code"] = new() { [AppLanguage.Arabic] = "كود التفعيل غير صالح", [AppLanguage.English] = "Invalid activation code" },
        ["license_expired"] = new() { [AppLanguage.Arabic] = "انتهت صلاحية هذه النسخة", [AppLanguage.English] = "This copy has expired" },
        ["license_run_limit"] = new() { [AppLanguage.Arabic] = "تم الوصول إلى الحد الأقصى لعدد مرات التشغيل", [AppLanguage.English] = "Run limit reached" },
        ["license_wrong_machine"] = new() { [AppLanguage.Arabic] = "كود التفعيل غير مخصص لهذا الجهاز", [AppLanguage.English] = "This activation code is not valid for this device" },
        ["your_machine_id"] = new() { [AppLanguage.Arabic] = "معرّف هذا الجهاز", [AppLanguage.English] = "This device's Machine ID" },
        ["copy"] = new() { [AppLanguage.Arabic] = "نسخ", [AppLanguage.English] = "Copy" },
        ["play"] = new() { [AppLanguage.Arabic] = "تشغيل", [AppLanguage.English] = "Play" },
        ["pause"] = new() { [AppLanguage.Arabic] = "إيقاف مؤقت", [AppLanguage.English] = "Pause" },
        ["next"] = new() { [AppLanguage.Arabic] = "التالي", [AppLanguage.English] = "Next" },
        ["previous"] = new() { [AppLanguage.Arabic] = "السابق", [AppLanguage.English] = "Previous" },
        ["protected_notice"] = new() { [AppLanguage.Arabic] = "هذا المحتوى محمي بحقوق الملكية — يُمنع النسخ أو التسجيل", [AppLanguage.English] = "This content is protected — copying or recording is prohibited" },
    };
}
