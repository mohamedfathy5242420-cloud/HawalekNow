# HawalekNow — حوالك ناو

منصة لاكتشاف المنتجات والتجار القريبين في مصر ومقارنة السعر والتوافر لكل فرع ثم التواصل مباشرة مع التاجر. البيع يتم خارج المنصة؛ لا سلة أو دفع أو شحن داخلها.

## بداية أي Agent

اقرأ [CURRENT_STATE](docs/CURRENT_STATE.md) أولًا، ثم [DECISIONS](docs/DECISIONS.md) و[SCOPE](docs/SCOPE.md) و[GIT_WORKFLOW](docs/GIT_WORKFLOW.md) و[ROADMAP](planning/ROADMAP.md)، وملف الخطوة والتسليم السابق المشار إليهما في الحالة. نفذ خطوة واحدة فقط بإذن المستخدم، وحدث نتائج التحقق الفعلية قبل التسليم.

## هيكل المستودع

| المسار | الغرض |
| --- | --- |
| [backend/](backend/README.md) | Solution ومشاريع Clean Architecture والاختبارات؛ تفاصيل التشغيل في backend/README |
| [docs/](docs/CURRENT_STATE.md) | حالة المشروع والقرارات والنطاق وقواعد Git |
| [planning/](planning/ROADMAP.md) | الخريطة والقوالب والخطوات والتسليم وفاحصا التوثيق |
| [docker/](docker/README.md) | مكان إعداد تشغيل الخدمات في P01-S06؛ لا Compose حاليًا |
| global.json | اختيار .NET SDK |

الواجهة مؤجلة؛ لم ينشأ مجلد frontend أو Angular أو أدواته. مرجعا المتطلبات وخطة Backend في جذر المستودع محفوظان دون تعديل.

## متطلبات التطوير

SDK المتاح والمختار .NET **10.0.401**. [global.json](global.json) يستخدم `latestPatch` داخل feature band `10.0.4xx` فقط، و`allowPrerelease: false`. لا انتقال تلقائي إلى band أحدث أو .NET major آخر. يفشل الاختيار عند غياب SDK متوافق؛ تغيير السياسة يحتاج مراجعة.

Git وPowerShell لتشغيل فحوص التوثيق. يلزم Docker Engine بوضع Linux containers للخدمات لاحقًا. وفق D009 اعتمد المستخدم تأجيل تشغيل وفحص Engine إلى P01-S06؛ فحصه مؤجل وليس ناجحًا، ولا يمنع إنشاء مشاريع .NET واختبارات المعمارية المحلية. لا SQL Server أو Mailpit أو CI حاليًا؛ API host فقط دون ميزات تجارية.

## الأوامر المتاحة الآن

من جذر المستودع:

```powershell
dotnet --version
dotnet sln backend/HawalekNow.slnx list
dotnet restore backend/HawalekNow.slnx
dotnet build backend/HawalekNow.slnx --no-restore
dotnet test backend/HawalekNow.slnx --no-build --no-restore
& ./planning/verify-roadmap.ps1
& ./planning/verify-git-workflow.ps1
git diff --check
```

[Backend](backend/README.md) يشرح الطبقات والاعتمادات وأمر تشغيل Api. [P01-S02](planning/phase-01/steps/S02.md) يسجل النتائج وحدود التحقق. Docker مؤجل وفق D009 إلى P01-S06. Git على هذا الجهاز يحتاج الخيار -c safe.directory=D:/HwalekNow بسبب اختلاف الملكية.
