# Backend

.NET 10 Clean Architecture داخل Monorepo؛ SDK يحكمه [global.json](../global.json) دون تغيير.

| المشروع داخل src | المسؤولية | Project References |
| --- | --- | --- |
| HawalekNow.Domain | قواعد المجال لاحقًا، مستقل عن الويب والتخزين | لا شيء |
| HawalekNow.Application | حالات الاستخدام والعقود لاحقًا | Domain |
| HawalekNow.Infrastructure | تنفيذ العقود والتكاملات لاحقًا | Application وDomain |
| HawalekNow.Api | HTTP Controllers وتركيب الخدمات | Application وInfrastructure |

[الحل](HawalekNow.slnx) يضم المشاريع الأربعة وtests/UnitTests وtests/ArchitectureTests وtests/IntegrationTests.
Domain وApplication وInfrastructure مكتبات فارغة حاليًا دون Class1 أو كود تجريبي. Api يستخدم AddControllers وMapControllers فقط؛ لا Controllers تجارية بعد، وبالتالي لا مسارات تطبيق مسجلة.

ArchitectureTests تفحص ProjectReference الفعلي والاتجاهات في Assemblies، وتمنع ASP.NET وEF Core في Domain. تجارب معزولة تحقن ProjectReference ممنوعًا وEF Core وASP.NET في نسخ مؤقتة وتثبت رفضها وبقاء الأصل دون تغيير.
UnitTests وIntegrationTests مجهزان بـxUnit دون اختبارات شكلية؛ سيضاف السلوك الحقيقي في خطواته. لا اختبارات Containers الآن.

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
Docker Engine وLinux containers مؤجلان إلى P01-S06 وفق D009 وليس Passed؛ لا تشغيل Docker ضمن S02. Modules وMediatR وPersistence وIdentity والواجهة والميزات التجارية خارج الخطوة.
