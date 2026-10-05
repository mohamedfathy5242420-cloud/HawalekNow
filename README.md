# HawalekNow — حوالك ناو

منصة لاكتشاف المنتجات والتجار القريبين في مصر ومقارنة السعر والتوافر لكل فرع ثم التواصل مباشرة مع التاجر. البيع يتم خارج المنصة؛ لا سلة أو دفع أو شحن داخلها.

## بداية أي Agent

اقرأ [CURRENT_STATE](docs/CURRENT_STATE.md) أولًا، ثم [DECISIONS](docs/DECISIONS.md) و[SCOPE](docs/SCOPE.md) و[GIT_WORKFLOW](docs/GIT_WORKFLOW.md) و[ROADMAP](planning/ROADMAP.md)، وملف الخطوة والتسليم السابق المشار إليهما في الحالة. نفذ خطوة واحدة فقط بإذن المستخدم، وحدث نتائج التحقق الفعلية قبل التسليم.

## هيكل المستودع

| المسار | الغرض |
| --- | --- |
| [backend/](backend/README.md) | Solution باسم HawalekNow؛ فارغة في P01-S01؛ المشاريع والاختبارات في P01-S02 |
| [docs/](docs/CURRENT_STATE.md) | حالة المشروع والقرارات والنطاق وقواعد Git |
| [planning/](planning/ROADMAP.md) | الخريطة والقوالب والخطوات والتسليم وفاحصا التوثيق |
| [docker/](docker/README.md) | مكان إعداد تشغيل الخدمات في P01-S06؛ لا Compose حاليًا |
| global.json | اختيار .NET SDK |

الواجهة مؤجلة؛ لم ينشأ مجلد frontend أو Angular أو أدواته. مرجعا المتطلبات وخطة Backend في جذر المستودع محفوظان دون تعديل.

## متطلبات التطوير

SDK المتاح والمختار .NET **10.0.401**. [global.json](global.json) يستخدم `latestPatch` داخل feature band `10.0.4xx` فقط، و`allowPrerelease: false`. لا انتقال تلقائي إلى band أحدث أو .NET major آخر. يفشل الاختيار عند غياب SDK متوافق؛ تغيير السياسة يحتاج مراجعة.

Git وPowerShell لتشغيل فحوص التوثيق. يلزم Docker Engine بوضع Linux containers للخدمات لاحقًا. وفق D009 اعتمد المستخدم تأجيل تشغيل وفحص Engine إلى P01-S06؛ فحصه مؤجل وليس ناجحًا، ولا يمنع إنشاء مشاريع .NET واختبارات المعمارية المحلية. لا SQL Server أو Mailpit أو API أو CI منفذة في هذه الخطوة.

## الأوامر المتاحة الآن

من جذر المستودع:

```powershell
dotnet --list-sdks
dotnet --version
dotnet sln backend/HawalekNow.slnx list
dotnet build backend/HawalekNow.slnx
& ./planning/verify-roadmap.ps1
& ./planning/verify-git-workflow.ps1
git diff --check
docker version
docker compose version
docker context show
docker info --format '{{.OSType}}'
```

الحل فارغ: فحص build لا يبني Backend ولا يختبر التطبيق، وقد يحذر من عدم وجود مشروع للاستعادة. لا أمر تشغيل API أو اختبارات أو `docker compose up` متاح بعد. على هذا الجهاز احتاج Git خيار `-c safe.directory=D:/HwalekNow` بسبب اختلاف الملكية؛ لا يلزم تغيير الإعداد العام.

المعمارية المستهدفة Clean Architecture داخل Modular Monolith حسب D001؛ تفاصيل التنفيذ والحزم والخدمات تأتي في خطواتها المعتمدة. [P01-S01](planning/phase-01/steps/S01.md) يسجل الأدلة والقيود الحالية.
