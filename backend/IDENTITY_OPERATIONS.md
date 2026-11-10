# تشغيل هوية HawalekNow محليًا

Updated: 2026-11-10
Status: InProgress

## Admin bootstrap

يعمل الأمر المقصود مرة لإنشاء Admin الأول، ويعيد التشغيل دون تكرار أو تعديل أي حساب قائم. لا يطبق Migration أو يعيد إنشاء قاعدة. جهّز قاعدة HawalekNow_Development عبر Migration المراجعة أولًا، ثم وفّر الإعدادات التالية من Environment أو مخزن أسرار محلي. لا تحفظ قيمًا حقيقية في ملف متتبع أو سجل أو Collection.

| متغير Environment | الاستخدام |
| --- | --- |
| ConnectionStrings__Application | اتصال SQL بقاعدة التطبيق المحددة؛ ليس master أو قاعدة مشروع آخر |
| Identity__AdminSeed__Username | اسم Admin الأول؛ 3–64 من الحروف والأرقام ASCII و._- |
| Identity__AdminSeed__Email | بريد Admin يحدده المشغل |
| Identity__AdminSeed__Password | كلمة مرور سرية من 15–128 حرفًا؛ لا توجد قيمة افتراضية |

```powershell
dotnet run --project backend/src/HawalekNow.Api --configuration Release --no-build --no-launch-profile -- --seed-admin
```

ينتهي الأمر قبل تشغيل HTTP. نجاحه exit 0؛ الإعداد الناقص أو الخاطئ أو تعذر SQL exit 1 برسالة ثابتة دون قيم أو استثناء. ينشئ bootstrap الموثوق Admin مؤكد البريد. وجود Admin يترك PasswordHash والهوية والصلاحيات كما هي، حتى عند تغير الإعدادات؛ تعديل حساب قائم يحتاج إجراء مستقل مقصود. لا تعرض اسم المشغل أو البريد أو كلمة المرور في report أو logs.

## التحقق المعزول

```powershell
./backend/test-p02.ps1
```

محليًا يقرأ المشغل إعداد SQL الموجود في docker/.env دون عرضه. في CI يمرر HAWALEKNOW_TEST_SQL باتصال خاص بالتشغيل وDatabase=master؛ الاختبارات تنشئ قاعدة HN_P02_Test_<Guid> جديدة، وتطبق Migration حقيقية، وتنظف قواعدها بالاسم والملكية فقط. لا EF InMemory أو EnsureDeleted. تبقى قاعدة التطبيق وS06 probe وVolumes وموارد المشاريع الأخرى محفوظة.

S01: Migration وSQL والمعاملات وRowVersion؛ S02: bootstrap والتنافس والإعدادات وProgram الحقيقي. تعليمات الرحلات تضاف عند تنفيذ خطواتها. هذا دليل تطوير واختبار، وليس ادعاء جاهزية إنتاج أو تحقق Brevo.
