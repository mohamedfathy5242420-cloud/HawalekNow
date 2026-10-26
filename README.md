# HawalekNow — حوالك ناو

P01-S06 Done وApproved؛ PR #7 مدموجة. P01-S07 InReview: [السجل](planning/phase-01/steps/S07.md) و[شرح OpenAPI وScalar وapi/v1 وHealth وPostman بالمصري](backend/API_EXPLORATION.md). S08 NotStarted وتسليمها Draft؛ اعتماد المستخدم ما زال Pending. الحالة الحالية في [CURRENT_STATE](docs/CURRENT_STATE.md).

منصة لاكتشاف المنتجات والتجار القريبين في مصر ومقارنة السعر والتوافر لكل فرع ثم التواصل مباشرة مع التاجر. البيع يتم خارج المنصة؛ لا سلة أو دفع أو شحن داخلها.

## بداية أي Agent

اقرأ [CURRENT_STATE](docs/CURRENT_STATE.md) أولًا، ثم [DECISIONS](docs/DECISIONS.md) و[SCOPE](docs/SCOPE.md) و[GIT_WORKFLOW](docs/GIT_WORKFLOW.md) و[ROADMAP](planning/ROADMAP.md)، وملف الخطوة والتسليم السابق المشار إليهما في الحالة. نفذ خطوة واحدة فقط بإذن المستخدم، وحدث نتائج التحقق الفعلية قبل التسليم.

## هيكل المستودع

| المسار | الغرض |
| --- | --- |
| [backend/](backend/README.md) | Solution ومشاريع Clean Architecture والاختبارات؛ تفاصيل التشغيل في backend/README |
| [docs/](docs/CURRENT_STATE.md) | حالة المشروع والقرارات والنطاق وقواعد Git |
| [planning/](planning/ROADMAP.md) | الخريطة والقوالب والخطوات والتسليم وفاحصا التوثيق |
| [docker/](docker/README.md) | Compose محلي لـSQL Server 2025 Full Text وMailpit؛ أوامر التشغيل والفحص |
| global.json | اختيار .NET SDK |

الواجهة مؤجلة؛ لم ينشأ مجلد frontend أو Angular أو أدواته. مرجعا المتطلبات وخطة Backend في جذر المستودع محفوظان دون تعديل.

## متطلبات التطوير

SDK المتاح والمختار .NET **10.0.401**. [global.json](global.json) يستخدم `latestPatch` داخل feature band `10.0.4xx` فقط، و`allowPrerelease: false`. لا انتقال تلقائي إلى band أحدث أو .NET major آخر. يفشل الاختيار عند غياب SDK متوافق؛ تغيير السياسة يحتاج مراجعة.

Git وPowerShell لتشغيل فحوص التوثيق. يلزم Docker Engine بوضع Linux containers للخدمات لاحقًا. فحص Engine الفعلي بدأ في P01-S06 وفق D009؛ نتائج SQL وFTS وMailpit تسجل في S06 بعد الاختبار. لا CI حاليًا؛ API host فقط دون ميزات تجارية.

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

[Backend](backend/README.md) يشرح الطبقات والاعتمادات وأمر تشغيل Api، و[Modules](backend/MODULES.md) يوضح الملكية وقواعد التواصل والمشترك. [P01-S04](planning/phase-01/steps/S04.md) Done بعد اعتماد 2026-10-13: CQRS Pipeline وتسجيل آمن وCorrelation ID، و65 اختبار معمارية وتكامل؛ [الحزم والتراخيص](backend/PACKAGES.md). الوحدات بلا وظائف تجارية. Docker ينفذ في P01-S06؛ نتائج الفحوص في سجلها. Git على هذا الجهاز يحتاج الخيار -c safe.directory=D:/HwalekNow بسبب اختلاف الملكية.
