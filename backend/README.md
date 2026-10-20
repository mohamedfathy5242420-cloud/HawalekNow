# Backend

S05 يؤسس [نتائج التطبيق وسياسة أخطاء HTTP والشرح خطوة بخطوة](API_ERRORS.md)؛ [ملف التنفيذ](../planning/phase-01/steps/S05.md) يسجل التحقق والمراجعة. الحالة الجارية في CURRENT_STATE.

.NET 10 Clean Architecture داخل Modular Monolith؛ SDK 10.0.401 يحكمه [global.json](../global.json).

| المشروع | المسؤولية | Project References |
| --- | --- | --- |
| Domain | قواعد المجال لاحقًا؛ مستقل عن الويب والتخزين | لا شيء |
| Application | حالات الاستخدام لاحقًا، وCQRS Pipeline التقني | Domain |
| Infrastructure | تنفيذ العقود والتكاملات لاحقًا | Application وDomain |
| Api | HTTP host وتركيب DI وSerilog وCorrelation ID | Application وInfrastructure |

[الحل](HawalekNow.slnx) يضم أربعة مشاريع إنتاج وUnitTests وArchitectureTests وIntegrationTests. [MODULES](MODULES.md) يحفظ ملكية التسع وحدات وD010/D011. الوحدات بلا Business types أو Contracts حاليًا؛ Shared في Application وApi له مستهلك فعلي موثق. Domain وInfrastructure بلا أنواع إنتاج. الأمثلة والـHandlers والـValidators في الاختبارات فقط، وMapControllers بلا endpoints تجارية أو تجريبية.

## تشغيل والتحقق

```powershell
 dotnet restore backend/HawalekNow.slnx --locked-mode
 dotnet build backend/HawalekNow.slnx --no-restore --disable-build-servers -m:1
 dotnet test backend/HawalekNow.slnx --no-build --no-restore --disable-build-servers -m:1
 dotnet run --project backend/src/HawalekNow.Api --no-build --no-restore --no-launch-profile --urls http://localhost:5080
 & ./planning/verify-roadmap.ps1
 & ./planning/verify-git-workflow.ps1
```

[الحزم والتراخيص](PACKAGES.md): MediatR 12.5.0، FluentValidation وDI extensions 12.1.1، Logging.Abstractions 10.0.0، Serilog 4.3.0 وHosting 10.0.0 وConsole 6.0.0؛ exact ranges وlockfiles. حزم الاختبارات السابقة محفوظة: Microsoft.NET.Test.Sdk 17.14.1 وxunit 2.9.3 وrunner 3.1.4 وcoverlet 6.0.4. لا حزمة ويب في Application أو حزمة جديدة في Domain.

## مسار الطلب والتسجيل الآمن

Program يستدعي AddApplication؛ التسجيل يفحص assembly Application، ويمكن إضافة assemblies معروفة من composition root؛ الاختبارات تضيف assembly الاختبارات فقط. لا فحص لكل assemblies المحملة أو module implementation dependencies في Shared.
ISender.Send يدخل Logging ثم Performance ثم Validation ثم Handler، والرجوع بالعكس. Performance يقيس مجموع التحقق والتنفيذ حتى لو فشل الطلب، دون threshold أو SLA تجاري مفترض. Validators transient وتنفذ بالتتابع بValidateAsync ونفس CancellationToken؛ أي فشل يمنع Handler. غياب Validator يسمح بالتنفيذ. الأخطاء والإلغاء تنتقل كما هي داخل Application؛ Api يحولها حسب سياسة S05.
Logger لا يسجل body أو response أو exception message/object أو ValidationFailure؛ يسجل RequestType وOutcome والمدة فقط داخل scope يحمل CorrelationId. Sink يقبل ثلاث templates ثابتة ومفاتيح وأنواع محددة ويمنع exceptions والخصائص الأخرى؛ السجلات العامة للـhost محجوبة في هذا الأساس المقصود، وتوسعتها تحتاج سياسة بيانات مراجعة. لا UseSerilogRequestLogging أو HTTP request logging أو configuration يعيد تفعيل providers أخرى. Console template يعرض CorrelationId، ولا منصة تجميع.
X-Correlation-ID يقبل قيمة واحدة من 1 إلى 64 حرف ASCII من A-Z/a-z/0-9/-/_؛ القيم الأخرى أو المتعددة تستبدل Guid عشوائي N بطول 32. Middleware يهيئ context scoped ويرجع نفس المعرف في header، وnon-HTTP Send يستخدم fallback scoped. المعرف metadata غير موثوق للأمان؛ ليس هوية أو صلاحية أو idempotency key، ولا نعامل تطابقه كدليل على نفس المستخدم.

## المعاملات والاختبارات والقيود

D004: Workflow Step تعني Command أو Query؛ MediatR لا ينشئ Transaction. يمكن تنفيذ عدة خطوات داخل Transaction واحدة، وعدة Transactions ليست Atomic تلقائيًا. الحدود تتحدد مع Use Case؛ لا UoW أو Transaction Behavior أو Orchestrator في S04.
47 ArchitectureTests تحمي ProjectReferences وAssembly وملكية المسارات وNamespaces واتجاهات الطبقات وعقود الوحدات مع تجارب رفض وسماح معزولة. Roslyn من SDK للاختبارات فقط؛ إصلاح namespace nullable للرموز المركبة لا يغير الحدود، واختبارا tuple يحفظان الرفض والسماح. static checks لا تثبت SQL أو reflection/dynamic.
18 IntegrationTests الأصلية تختبر Send وDI وValidators والـBehaviors والسرية والمدة والفشل والإلغاء وHTTP middleware وعزل scoped requests متزامنة. S05 بعد تصحيح مراجعة null بتاريخ 2026-10-19 تشمل 23 حالة HTTP TestServer و7 UnitTests لـResult، فيكون الحل 95 اختبارًا: 47 Architecture و41 Integration و7 Unit، بلا failed/skipped. اختبار null أثبت الفشل قبل الإصلاح والنجاح بعده؛ Result<T>.Success(null) يرجع 200 وapplication/json والـbody JSON null، بينما Result.Success() يظل 204. فحص host التاريخي في S04 أكد 404 وCorrelation ID فقط؛ اختبارات S05 تثبت API error contract دون إضافة endpoints إنتاج أو رحلة Business.
[S04](../planning/phase-01/steps/S04.md) Done بعد اعتماد المستخدم بتاريخ 2026-10-13 و[تسليم S05](../planning/phase-01/handoffs/S04-to-S05.md) Ready؛ P01-S05 تنفذ بإذن 2026-10-14 دون دمج. Docker Engine وLinux containers مؤجلان إلى P01-S06 وفق D009 وليس Passed؛ لا تشغيل Docker. التخزين والمعاملات في P02-S01.
