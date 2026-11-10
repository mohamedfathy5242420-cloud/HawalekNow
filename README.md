# HawalekNow — حوالك ناو

P01 Done ومراجعتها Approved بقرار المستخدم D013 بتاريخ 2026-11-10، المشروط بنجاح فحوص الإغلاق قبل دمج PR #8 وبعده. [سجل الإغلاق](planning/phase-01/CLOSURE.md) و[مراجعة المرحلة](planning/phase-01/PHASE_REVIEW.md) و[الحالة الحالية](docs/CURRENT_STATE.md). تسليم P02 Ready وP02 NotStarted دون تنفيذ. تجربة المستخدم الشخصية Scalar/Postman والشرح الكامل Deferred عن بوابة الإغلاق بقراره، وليست Passed؛ [تعليمات التجربة](backend/API_EXPLORATION.md) محفوظة.

منصة لاكتشاف المنتجات والتجار القريبين في مصر ومقارنة السعر والتوافر لكل فرع ثم التواصل مباشرة مع التاجر. البيع يتم خارج المنصة؛ لا سلة أو دفع أو شحن داخلها.

## بداية أي Agent

اقرأ [CURRENT_STATE](docs/CURRENT_STATE.md) ثم [DECISIONS](docs/DECISIONS.md) و[SCOPE](docs/SCOPE.md) و[GIT_WORKFLOW](docs/GIT_WORKFLOW.md) و[ROADMAP](planning/ROADMAP.md)، وملف الخطوة والتسليم السابق. وفق D012 نفذ خطوات المرحلة المأذونة بالتتابع بعد نجاح التحقق التقني؛ المراجعة والاعتماد على مستوى المرحلة. لا تبدأ التالية أو تدمج قبل إذنها واعتماد السابقة.

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

Git وPowerShell 7 لفحوص التوثيق، وDocker Linux/amd64 للخدمات والصور. نتائج SQL/FTS/Mailpit والاستمرار التاريخية في S06؛ لا تفترض صحة خدمة متوقفة. [Workflow P01](.github/workflows/p01-baseline.yml) يتحقق على GitHub دون أسرار أو نشر؛ نتائجه الفعلية في مراجعة المرحلة. API بلا ميزات تجارية.

## الأوامر المتاحة الآن

من جذر المستودع:

```powershell
dotnet --version
dotnet sln backend/HawalekNow.slnx list
dotnet restore backend/HawalekNow.slnx --locked-mode
dotnet build backend/HawalekNow.slnx --no-restore
dotnet test backend/HawalekNow.slnx --no-build --no-restore
dotnet format backend/HawalekNow.slnx --verify-no-changes --no-restore
& ./planning/verify-roadmap.ps1
& ./planning/verify-git-workflow.ps1
& ./planning/test-phase-gates.ps1
git diff --check
```

[Backend](backend/README.md) يشرح الطبقات والتشغيل، و[Modules](backend/MODULES.md) يوضح الملكية، و[الحزم والتراخيص](backend/PACKAGES.md) تسجل النسخ والتوافق. الحل الحالي يضم 126 اختبارًا، والوحدات بلا وظائف تجارية. خدمات Docker وFTS والاستمرار في S06 لها أدلة تاريخية؛ حالتها الحالية في مراجعة P01. Git على هذا الجهاز يحتاج -c safe.directory=D:/HwalekNow لاختلاف الملكية؛ الفاحص يحسب المسار الفعلي ليعمل على Linux وWindows.
