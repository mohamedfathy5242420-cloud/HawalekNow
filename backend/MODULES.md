# ملكية الوحدات وحدود التواصل

OwnerStep: P01-S03
Updated: 2026-10-11

المصدر: [المتطلبات المعتمدة](../Hawalek_Now_Approved_Requirements_AR.docx)، الأقسام المذكورة أدناه؛ [خطة التنفيذ](../Hawalek_Now_Backend_Master_Execution_Plan_AR.docx)، P01-S03 ومراحل P02–P08؛ [القرارات](../docs/DECISIONS.md)، D001 وD003/D004 وD010/D011. هذه خريطة ملكية؛ الوظائف والعقود لم تنفذ بعد.

| Module | الملكية والمسؤولية | المصدر والحد الفاصل |
| --- | --- | --- |
| Identity | حساب الدخول وEmail/Password وتأكيد البريد وOTP والجلسات وصلاحيات Admin | المتطلبات 2–3 و11؛ P02؛ D005/D010/D011. لا يملك MerchantAccount أو موافقة التاجر |
| Merchants | MerchantAccount وحالات اعتماد التاجر والمتجر والفروع ونقاط الاستلام والمواعيد ووسائل الاتصال | المتطلبات 3–4؛ P02-S01 وP04؛ D010. تنفيذ نموذج حساب التاجر في P02 لا ينقل ملكيته إلى Identity. لا يملك أسعار المنتجات لكل فرع |
| Catalog | الأنشطة والتصنيفات والمنتجات وصورها ودورة النشر وProductListing بسعر وتوافر لكل فرع | المتطلبات 5؛ P03-S01/P03-S02 وP05. الفرع نفسه مملوك لـMerchants؛ Locations يملك التقسيم الإداري للمكان |
| Locations | المحافظات والمدن والمناطق والتسلسل الإداري والبيانات المرجعية الجغرافية ثنائية اللغة | المتطلبات 6 و10؛ P03-S03/P03-S04. موقع الفرع وبياناته في Merchants؛ حساب النطاق والمسافة للنتائج في Search |
| Search | اكتشاف المنتجات والمتاجر والتصنيفات والتطابق والترتيب والنطاق والمسافة والمرادفات المدارة إداريًا | المتطلبات 6 و10–11؛ P03-S05 وP06. لا يملك المنتج أو المتجر أو التصنيف؛ يقرأ عبر عقود التطبيق عند تأسيسها |
| Favorites | حفظ المنتجات والمتاجر وقائمتا العميل المستقلتان وملكية المفضلة | المتطلبات 8؛ P07-S01/P07-S02؛ D011. لا حجز أو شراء أو نسخ ملكية العناصر المحفوظة |
| Reports | بلاغات المنتجات ونقاط الاستلام والأسباب والمرفقات وخصوصية المبلغ والمراجعة اليدوية | المتطلبات 8 و11؛ P07-S03/P07-S04/P07-S05؛ D011. إجراء الإخفاء أو التعليق ينفذ داخل الوحدة صاحبة المحتوى، ولا ينفذ تلقائيًا بسبب عدد البلاغات |
| Notifications | الإشعارات التشغيلية للمستخدم والتاجر المنصوص عليها، ومنها إشعار البلاغ الصحيح | المتطلبات 8؛ P07-S06؛ D011. Identity يظل صاحب OTP وقواعد التأكيد والاسترجاع؛ نقل رسالة أو إرسال بريد لا ينقل ملكية العملية إلى Notifications |
| Analytics | أحداث الظهور والمشاهدات وPhone/WhatsApp والمفضلة والأرقام المجمعة والتتبع المجهول | المتطلبات 9؛ P08-S05/P08-S06/P08-S07؛ D011. لا يدمج النشاط المجهول بالحساب لاحقًا؛ لا هوية زائر للتاجر، ولا احتساب الاتجاهات في لوحة التاجر |

عمليات Admin الخاصة بالموافقات والتصنيفات والمرادفات والبلاغات والمحتوى والإحصاءات موجودة في الوحدة صاحبة البيانات. Admin صلاحية داخل Identity وليست Module. Audit يسجل فعل الإدارة؛ لا يملك العملية التجارية. الخدمات التشغيلية مثل Outbox وHangfire وLogging والتخزين ليست وحدات تجارية جديدة؛ تتأسس في توقيتها المحدد في Q003-01 إلى Q003-06.

## الطبقات والمجلدات وNamespaces

داخل كل مشروع إنتاج توجد `Modules/<Module>/README.md` لتثبيت المجلد وNamespace `HawalekNow.<Layer>.Modules.<Module>`. اختيار `<Layer>` من Domain وApplication وInfrastructure وApi فقط. الـREADME يحجز Namespace ويحدد دور الطبقة؛ لا أنواع compiled في الوحدات حاليًا، ولا Marker أو Base classes لإعطاء مظهر تنفيذ غير موجود.
أي ملف C# لاحق داخل مجلد الوحدة يعلن Namespace مطابقًا لمسار المجلد؛ Namespace فرعي يعني مجلدًا فرعيًا مطابقًا. لا يسمح بتعريف نوع الوحدة في جذر المشروع أو وحدة أخرى. لا يغير namespace مكان ملكية البيانات. الـProject References الحالية ثابتة:

| المشروع | يعتمد على |
| --- | --- |
| Domain | لا مشاريع إنتاج |
| Application | Domain فقط |
| Infrastructure | Application وDomain |
| Api | Application وInfrastructure |

`Api/Program.cs` هو composition root الحالي؛ يركب خدمات Application وInfrastructure. يُستثنى من قاعدة مجلد الوحدة وNamespace فقط، لكنه يخضع لفحص مراجع الطبقات؛ ليس منفذًا لنقل Business logic إلى Api أو للوصول المباشر إلى Domain.

## قواعد التواصل وملكية الكود

- Domain الخاص بوحدة يعتمد على Domain نفس الوحدة والمشترك المثبت عند وجوده؛ لا يعتمد على Domain وحدة أخرى أو نوع Application.
- Application الخاص بوحدة يستخدم Domain نفس الوحدة وأنواع تطبيقها. التواصل مع وحدة أخرى يتم عبر عقود صريحة في `Application/Modules/<Owner>/Contracts` وNamespace المناظر `.Contracts` فقط. العقود مملوكة للوحدة المقدمة وتعرض بيانات مستقلة عن Entities أو Infrastructure؛ لا مسار Contracts مفتوحًا في Domain أو Infrastructure أو Api.
- Infrastructure ينفذ عقود الوحدة ويستخدم Domain وApplication الخاصين بها؛ لا يصل لتنفيذ Infrastructure أو Domain وحدة أخرى. عند احتياج عقد وحدة أخرى، يستخدم عقد Application الصريح نفسه دون تجاوز الملكية.
- Api الخاص بوحدة يستخدم Application نفس الوحدة وعقود Application العامة للوحدات الأخرى عند الحاجة، وليس أنواع Domain أو تفاصيل Infrastructure. تركيب الخدمات في Program هو المكان المخصص للاعتماد على Infrastructure.
- Contracts ليست موجودة حاليًا؛ لا Interfaces أو DTOs افتراضية أو Commands/Queries أو Events شكلية. ينشأ العقد عند وجود استخدام حقيقي ويُراجع كشفه للبيانات والصلاحيات. الاختبارات تحفظ موضعه وقواعده للمستقبل، ولا تدعي وجود تواصل Runtime بين الوحدات الآن.
- تغيير بيانات وحدة يتم داخل حالاتها وعقودها، لا بالتعديل المباشر على Entity أو Repository وحدة أخرى. D003 يثبت DbContext واحدًا وSchemas منطقية؛ الوحدة ليست قاعدة بيانات أو Transaction مستقلة. D004 يحدد حد المعاملة لكل Use Case عند تنفيذه، وOutbox عند الحاجة وفق قرارات التوقيت؛ لا Bus أو Orchestrator عام هنا.

مثال الملكية دون تنفيذ: Identity يثبت تأكيد البريد؛ Merchants يملك Pending/Approved. التاجر المؤكد وPending يستطيع الدخول وتجهيز بياناته، لكن المتجر والفروع والمنتجات لا تظهر للجمهور قبل Approved وفق D010. تنفيذ شرط الظهور في حالات الاستخدام اللاحقة لا ينسخ منطق اعتماد التاجر إلى Shared أو Search أو Catalog.

## المشترك الحقيقي

المشترك مسؤولية تقنية أو مفهوم مستقل عن أي وحدة، له احتياج مثبت وقرار وملكية واضحة؛ تشابه أسماء أو استخدام نوع في وحدتين لا يكفي. Entity أو قاعدة اعتماد التاجر أو التصنيف أو OTP تظل في وحدتها. لا تجريدات EntityBase أو RepositoryBase أو Result أو IClock أو IFileStorage جديدة في S03.
S03 لم تضف Shared؛ S04 تضيف المشترك التقني المثبت أدناه. عند إثبات الحاجة في خطوة لاحقة، يمكن إنشاء `Shared` داخل الطبقة المناسبة فقط مع Namespace `HawalekNow.<Layer>.Shared`؛ لا مشروع Shared جديد. المشترك لا يعتمد على Modules، والوحدة تعتمد على Shared باتجاهات الطبقات نفسها فقط. كل إضافة تذكر مستهلكها الحقيقي واختبارها وقرارها، ولا تفتح الباب لنسخ أنواع وحدات إليها.
Result/ProblemDetails في P01-S05، Persistence في P02-S01، Audit في P04-S02، وIFileStorage في P04-S03 وفق القرارات؛ لا يبدأ أي منها هنا. SDK والمعمارية ثابتان وDocker مؤجل إلى P01-S06 وفق D009 وليس Passed.

## ما يحميه الفاحص وحدوده

[ModuleBoundaryTests](tests/ArchitectureTests/ModuleBoundaryTests.cs) تستخدم [ModuleBoundaryInspector](tests/ArchitectureTests/ModuleBoundaryInspector.cs) لفحص قائمة المجلدات وNamespaces الموثقة، وكل مصدر C# فعلي خارج bin/obj، وتطابق Namespace مع ملكية مساره. تستخدم Roslyn compiler من الـSDK المثبت لتحليل الرموز والأنواع ومراجعها بما فيها الأسماء الكاملة وaliases وglobal usings والوراثة وAttributes وGenerics وتواقيع الأعضاء. لا PackageReference جديد؛ اعتماد compiler محصور في مشروع الاختبارات ومساراته مشتقة من MSBuildSDKsPath.
فحص الطبقات يحمي source dependencies أيضًا، بما فيها وصول Api المباشر إلى Domain عبر مرجع transitive. العقود العابرة للوحدات محصورة في Application Contracts؛ لا تكشف Entities حتى في نفس الوحدة. مصادر مخالفة سليمة compilation في نسخ ذاكرة معزولة تثبت الرفض، والمصدر الحقيقي لا يعدل. baseline سليم قبل كل تجربة، وتجارب سماح تثبت أن العقد الصحيح واتجاهات الطبقات الطبيعية مقبولان.
هذا فحص static مع compilation تحليلي داخل الاختبارات، وليس MSBuild بديلًا؛ Restore وBuild وفحوص ProjectReference/Assembly المستقلة تظل مطلوبة. لا يثبت SQL أو runtime workflows أو صحة الأعمال أو reflection/dynamic/string-based access أو queries على جداول وحدة أخرى؛ تضاف اختبارات مناسبة عند وجود الاستخدام. المجلدات لا تصنع عزل Assemblies، والوحدات لا تحتوي وظائف بعد. IntegrationTests تختبر CQRS وDI والتسجيل وHTTP middleware في S04؛ UnitTests لم تضف اختبارات بعد.

## المشترك التقني المؤسس في S04

OwnerStep: P01-S04؛ D002/D006 وQ003-05. لا تغيير لملكية الوحدات أو D010/D011.

| الموضع | المسؤولية | المستهلك الفعلي ودليل الحاجة |
| --- | --- | --- |
| Application/Shared/DependencyInjection.cs | تسجيل MediatR وValidators وترتيب Behaviors | Api/Program.cs يستدعي AddApplication؛ IntegrationTests تستدعي نفس التسجيل مع assembly الاختبارات |
| Application/Shared/Behaviors | Validation وLogging وPerformance لكل Send | MediatR DI يبنيها فعليًا في 18 اختبار تكامل؛ production host مسجل لكن لا Business requests بعد |
| Application/Shared/Diagnostics | CorrelationContext scoped وtemplates تقنية ثابتة | LoggingBehavior يستهلك context وPerformanceBehavior يستهلك templates؛ Api middleware يهيئ context وfilter يستخدم المخطط |
| Api/Shared/Diagnostics | HTTP correlation وSerilog sink policy | Program يركب middleware وإعداد السجل؛ IntegrationTests تختبرهما دون endpoints إنتاج |

Application يستخدم ILogger abstraction ولا يعرف Serilog أو HttpContext؛ Api يربط implementation. CorrelationContext مفهوم تقني scoped ذو استخدام فعلي في middleware والـPipeline، دون interface بلا implementation. Shared لا يعتمد على Modules؛ لا Domain Shared أو Infrastructure Shared أو مشروع جديد. الأمثلة والـHandlers والـValidators التجريبية في IntegrationTests فقط. لا Contracts تجارية أو معاملات أو تخزين.
فاحص المعمارية عالج namespace nullable للرموز المركبة من Roslyn مع استمرار فحص TypeArguments وContainingType؛ تجارب tuple سليمة ومخالفة تثبت استمرار المنع. قواعد الطبقات والوحدات لم تخفف.

## المشترك التقني المؤسس في S05

OwnerStep: P01-S05؛ D002 وQ003-05. Application/Shared/Results يملك Result وResult<T> وErrorKind/Error بدون HTTP؛ Api/Shared/Errors مستهلكه الحقيقي ويحوّله إلى IActionResult وProblemDetails. IExceptionHandler وMVC factory وstatus pages تقنية HTTP داخل Api فقط، وتستخدم CorrelationContext scoped الموجود. UnitTests تختبر invariants، وIntegrationTests تستعمل handlers/controllers داخل test assembly فقط عبر TestServer وتسجيل الإنتاج نفسه؛ لا اختبار assembly في Program. Domain/Infrastructure ووحدات Business بلا إضافات؛ لا تغيير للـProjectReferences أو قواعد الفاحص المعماري.
Api/Shared/Diagnostics/RemoveHostMetadata يحذف RequestId/RequestPath/ConnectionId/ActionId/ActionName المضافة من scopes الخاصة بـASP.NET قبل فلتر S04؛ قائمة الأحداث والخصائص المسموحة لم تتوسع. الرفض للأسرار والخصائص الأخرى والاستثناءات محفوظ، ولا HTTP metadata في السجل. السجلات العامة محجوبة والتشخيص التشغيلي غير مكتمل.
