# Backend

.NET 10 Clean Architecture داخل Monorepo؛ SDK يحكمه [global.json](../global.json) دون تغيير.

| المشروع داخل src | المسؤولية | Project References |
| --- | --- | --- |
| HawalekNow.Domain | قواعد المجال لاحقًا، مستقل عن الويب والتخزين | لا شيء |
| HawalekNow.Application | حالات الاستخدام والعقود لاحقًا | Domain |
| HawalekNow.Infrastructure | تنفيذ العقود والتكاملات لاحقًا | Application وDomain |
| HawalekNow.Api | HTTP Controllers وتركيب الخدمات | Application وInfrastructure |

[الحل](HawalekNow.slnx) يضم المشاريع الأربعة وtests/UnitTests وtests/ArchitectureTests وtests/IntegrationTests.
Domain وApplication وInfrastructure بلا أنواع إنتاج حاليًا. في كل طبقة مجلدات التسع Modules مع ملفات ملكية وNamespaces محجوزة، دون Marker classes. Api يستخدم AddControllers وMapControllers فقط؛ لا Controllers تجارية بعد، وبالتالي لا مسارات تطبيق مسجلة.

[حدود الوحدات](MODULES.md) تحدد Identity وMerchants وCatalog وLocations وSearch وFavorites وReports وNotifications وAnalytics ومصادرها واتجاهات التواصل والمشترك الحقيقي. D010 يفصل تأكيد البريد في Identity عن MerchantAccount واعتماد التاجر في Merchants؛ D011 يثبت الوحدات الإضافية وعمليات Admin داخل الوحدة صاحبة البيانات، وصلاحية Admin داخل Identity. العقد بين الوحدات يؤسس داخل Application/Modules/Owner/Contracts عند الاستخدام؛ لا عقود أو Shared أو وظائف Business مضافة الآن.

ArchitectureTests تفحص ProjectReference الفعلي والاتجاهات في Assemblies، وتمنع ASP.NET وEF Core في Domain. تجارب معزولة تحقن ProjectReference ممنوعًا وEF Core وASP.NET في نسخ مؤقتة وتثبت رفضها وبقاء الأصل دون تغيير.
UnitTests وIntegrationTests مجهزان بـxUnit دون اختبارات شكلية؛ سيضاف السلوك الحقيقي في خطواته. لا اختبارات Containers الآن.

ModuleBoundaryTests تفحص قائمة المجلدات والـNamespaces المحجوزة وتطابق أي مصدر C# مع المجلد والوحدة، واتجاهات اعتماد الأنواع وعقود Application وعدم اعتماد Shared على الوحدات. تستخدم Roslyn الموجود في SDK عبر References في مشروع الاختبارات فقط، دون تغيير SDK أو حزم NuGet. التجارب داخل الذاكرة تبدأ من baseline سليم، وتشمل السماح بعقد صحيح ورفض Entities وحدات أخرى وaliases والأسماء الكاملة وContracts التي تكشف التنفيذ ووصول Api المباشر إلى Domain أو Infrastructure خارج Program. أنواع التجارب لا تدخل مشاريع الإنتاج. المجلدات ليست عزل Assemblies؛ لا تواصل Runtime أو اختبارات سلوك لوحدات فارغة. بعد Build، الفحص التحليلي يستخدم GlobalUsings المولدة؛ فشل compilation التحليلي يفشل الاختبار ولا يسجل Passed.

من جذر المستودع:

```powershell
dotnet restore backend/HawalekNow.slnx
dotnet build backend/HawalekNow.slnx --no-restore
dotnet test backend/HawalekNow.slnx --no-build --no-restore
dotnet run --project backend/src/HawalekNow.Api --no-launch-profile --urls http://localhost:5080
& ./planning/verify-roadmap.ps1
& ./planning/verify-git-workflow.ps1
```

حزم مشاريع الاختبارات فقط، من قالب SDK المثبت: Microsoft.NET.Test.Sdk 17.14.1، xunit 2.9.3، xunit.runner.visualstudio 3.1.4، coverlet.collector 6.0.4. لا NuGet packages في مشاريع الإنتاج. ASP.NET يأتي من Web SDK وshared framework.
Docker Engine وLinux containers مؤجلان إلى P01-S06 وفق D009 وليس Passed؛ لا تشغيل Docker ضمن S03. MediatR وPipeline وResult/ProblemDetails وPersistence وتنفيذ Identity والواجهة والميزات التجارية تنتظر خطواتها. [S03](../planning/phase-01/steps/S03.md) Done بعد اعتماد المستخدم بتاريخ 2026-10-11 ويسجل نتائج التحقق، و[تسليم S04](../planning/phase-01/handoffs/S03-to-S04.md) Ready؛ P01-S04 NotStarted وبلا ملف تنفيذ.
