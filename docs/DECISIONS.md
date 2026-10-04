# سجل قرارات HawalekNow

آخر تحديث: 2026-10-04

## مصادر القرارات

- المتطلبات: `Hawalek_Now_Approved_Requirements_AR.docx`.
- خطة التنفيذ: `Hawalek_Now_Backend_Master_Execution_Plan_AR.docx`.
- تصحيحات المستخدم اللاحقة تسجل هنا وتنعكس على الخطوات المتأثرة.

## D001 المعمارية والنطاق التقني

الحالة: معتمد في المناقشة والخطة.

Clean Architecture داخل Modular Monolith وMonorepo. المشاريع: Domain وApplication وInfrastructure وApi.
Application يعتمد على Domain وعلى abstractions، ولا يعتمد على Infrastructure.
Infrastructure ينفذ العقود، وApi يربط الخدمات عبر Dependency Injection.
.NET 10 وControllers وEF Core 10 وSQL Server 2025 هي الاختيارات المثبتة في الخطة.
Angular للواجهة، وتفاصيل تنفيذ الواجهة مؤجلة.

## D002 CQRS والتحقق والأخطاء

الحالة: معتمد في المناقشة والخطة.

MediatR 12.5.0 مثبت ولا يرقى تلقائيًا. FluentValidation 12، مع Pipeline Behaviors للتحقق والتسجيل والأداء.
Result Pattern وProblemDetails وGlobal Exception Handler لتوحيد نتائج وأخطاء API.

## D003 التخزين والمعاملات

الحالة: معتمد في المناقشة والخطة.

DbContext واحد مع Schemas منطقية، وSpecialized Repositories وQuery Services وUnit of Work.
يمكن استخدام Generic Repository كقاعدة داخلية لإعادة استخدام العمليات العامة، دون تحويل UoW إلى مدخل مفتوح لكل الجداول.
تستخدم Guid v7 للمعرفات وRowVersion عند الحاجة للتعامل مع التعديلات المتزامنة.

## D004 تنسيق العمليات متعددة الخطوات

الحالة: معتمد بتوجيه المستخدم بتاريخ 2026-10-01.

كلمة Step في Workflow داخل Application تعني Command أو Query؛ وهي مختلفة عن Step التنفيذية في الخطة مثل P01-S04.
يمكن للعملية أن تضم عدة عمليات قاعدة بيانات داخل Transaction واحدة لتكون Atomic.
تستخدم Orchestrator أو Process Manager عند الحاجة إلى Workflow ينسق خطوات متعددة؛ ولا يفترض أن كل Command يفتح Transaction مستقلة.
الـQuery عادة للقراءة ولا تستدعي الحفظ. يحدد حد المعاملة لكل Use Case عند تصميمه.
Workflow الذي يمتد عبر أكثر من Transaction لا يصبح Atomic بمجرد استخدام Orchestrator؛ يحتاج حالة متابعة وإعادة محاولة ومنع تكرار، وتعويض عند الحاجة.
عمليات Email والخدمات الخارجية لا تتراجع تلقائيًا مع SQL Transaction. تستخدم Outbox للربط الموثوق حين يلزم.
الأثر: تؤخذ هذه القاعدة في P01-S04 وتطبق عند تصميم حالات الاستخدام؛ لا يلزم إنشاء Orchestrator عام قبل وجود احتياج فعلي.

## D005 الهوية والحماية

الحالة: معتمد في المناقشة والخطة.

ASP.NET Core Identity مع ApplicationUser واحد ونوع حساب، وMerchantAccount لبيانات وحالة التاجر.
فصل حساب العميل والتاجر، مع Admin واحد كدور في النسخة الأولى.
Email OTP للتأكيد والاسترجاع، وJWT وRefresh Token في HttpOnly Secure Cookies، مع CSRF وRate Limiting.
Mailpit للتطوير وBrevo API للإنتاج بحسب الخطة؛ إعداد الإرسال يشرح ويختبر وقت التنفيذ.

## D006 الخدمات المساندة

الحالة: مثبت في خطة التنفيذ.

- Hangfire Core مع SQL Server وTransactional Outbox.
- Serilog وCorrelation ID مع تأجيل منصة تجميع Logs.
- Magick.NET ومعالجة الصور وIFileStorage؛ تخزين محلي للتطوير واختيار Object Storage لاحقًا.
- IMemoryCache مع تأجيل Redis.
- SQL Server Full Text Search ومرادفات إدارية، وgeography مع NetTopologySuite للبحث المكاني.
- OpenAPI وScalar وPostman ومسارات API v1.
- xUnit وShouldly وNSubstitute وWebApplicationFactory وTestcontainers.
- Docker وGitHub Actions للتحقق الآلي.

## D007 أسلوب التنفيذ والتعلم

الحالة: معتمد.

نعمل على خطوة تنفيذية واحدة في كل مرة، مع شرح المفاهيم الجديدة وتطبيقها ومراجعتها مع المستخدم.
توثق الخطوة Scope والاعتمادات والاختبارات وDefinition of Done وHandoff.
V1 فحص ثابت، V2 اختبارات آلية، V3 تكامل، V4 مراجعة سلوك يدوي؛ يبرر عدم تطبيق أي طبقة غير مناسبة.
لا تنتقل الخطوة إلى Done لمجرد نجاح Build، ولا يبدأ التالي قبل المراجعة المطلوبة.

## D008 المستودع والمعرفات

الحالة: معتمد.

اسم المشروع HawalekNow. Remote: https://github.com/mohamedfathy5242420-cloud/HawalekNow.git.
معرف المرحلة P اختصار Phase، ومعرف الخطوة S اختصار Step.
قواعد الفروع والـCommit والـPR التفصيلية تعتمد في P00-S04.

## قرارات توزيع التأسيس المعتمدة في P00-S03

اعتمد المستخدم التوزيع التالي بتاريخ 2026-10-01 بعد مراجعة التقرير. المعرفات Q003-01 إلى Q003-06 محفوظة للتتبع، وحالة كل منها Approved. هذه القرارات تصحح توقيت التأسيس في مستند Word الأصلي الذي لم يُعدّل؛ التنفيذ الفعلي مؤجل لخطواته. D001–D008 تظل كما هي.

### Q003-01 Hangfire

Status: Approved
FoundationStep: P02-S04
CompletionStep: P08-S03

تأسيس الحد الأدنى مع إرسال OTP في P02-S04: SQL storage وWorker، وإعادة المحاولة والتعامل مع التكرار، واختبار rollback وإعادة التشغيل مع Outbox وفق Q003-02. قبل استخدام الجدولة في P02-S09 والحذف في P05-S08 والإشعارات في P07-S06 تُختبر السيناريوهات الخاصة بها. P08 تستكمل التغطية والتعافي والمراقبة، وP08-S03 تستكمل المعالجة وRetry وDead-letter.

### Q003-02 Outbox وحدود ضمان التسليم

Status: Approved
FoundationStep: P02-S04
CompletionStep: P08-S01, P08-S02, P08-S03

تأسيس Outbox مع OTP في P02-S04: حفظ نية الإرسال مع البيانات في نفس Transaction، وعقد الحد الأدنى للأحداث/الرسائل والعامل، وإعادة المحاولة والتعامل مع التكرار واختبار rollback وإعادة التشغيل. لا يكفي إرسال job بعد SaveChanges لتحقيق هذا الربط. P07-S06 تستخدم الأساس الموجود، وP08 تستكمل تغطية الأحداث والتعافي والمراقبة.
حدود ضمان التسليم: منع تكرار المعالجة لا يضمن عدم تكرار Email عند فشل الشبكة. قد يقبل مزود البريد الرسالة ثم تضيع الاستجابة، فتؤدي إعادة المحاولة إلى إرسال مكرر؛ لا ندعي exactly-once للبريد. حفظ نية الإرسال ذريًا يمنع فقدها بين حفظ البيانات والتسجيل، لكنه لا يضمن وصول البريد لصندوق المستلم. تُوثق حدود مزود البريد وسلوك إعادة المحاولة وتُختبر حالة القبول مع فقد الاستجابة في P02-S04. D004 يظل ملزمًا؛ عملية الإرسال الخارجية لا تتراجع مع SQL Transaction.

### Q003-03 Audit

Status: Approved
FoundationStep: P04-S02
CompletionStep: P08-S04

تأسيس سجل فعلي مع أول موافقة على تاجر في P04-S02، يشمل الفاعل ومعنى العملية والسبب والوقت، ويحفظ ذريًا مع التغيير ويحجب الأسرار. تتحقق الخطوة من السجل والصلاحيات وrollback وحجب الأسرار. P04-S07 وP05-S09 تستخدم السجل المؤسس. P08-S04 تستكمل التغطية وService/Interceptor والقيم قبل وبعد.

### Q003-04 الصور والتخزين

Status: Approved
FoundationStep: P04-S03
CompletionStep: P05-S05, P05-S06

تأسيس IFileStorage وتنفيذ محلي آمن بالحد اللازم لرفع لوجو المتجر في P04-S03، مع التحقق من الملفات واستمرار التخزين واختبار restart وpath safety. P05-S05 توسع معالجة صور المنتجات والتحقق منها، وP05-S06 توسع تغطية التخزين واختباراته. اختبر سياسة النشر في P05-S04 ببيانات اختبار، ثم اختبر رحلة الرفع والنشر كاملة بعد تجهيز الصور والتخزين في P05-S05/P05-S06؛ لا يُدعى اكتمال رحلة فعلية في اختبار السياسة وحده.

### Q003-05 Logging وCorrelation ID

Status: Approved
FoundationPhase: P01
CompletionStep: P08-S09

تأسيس Logging وCorrelation ID في P01 قبل أول استخدام يحتاجهما، مع حجب الأسرار من البداية. يجب أن يكون الأساس جاهزًا عند أول Logging/PerformanceBehavior في P01-S04 وأول API يحتاج التتبع. لا تؤجل حماية كلمات المرور وOTP وTokens إلى P08. P08-S09 تستكمل التغطية والتتبع والمراقبة. لا يثبت هذا القرار خدمات أو مكتبات إضافية خارج D002/D006.

### Q003-06 Persistence

Status: Approved
FoundationStep: P02-S01

تأسيس DbContext الواحد وSchemas وUoW والعقود المطلوبة في P02-S01 مع أول تخزين فعلي. تتوسع Repositories وQuery Services عند الحاجة، مع الحفاظ على D003 وD004 وعدم إنشاء تجريدات بلا استخدام. اختبارات Migration وحد المعاملة والحفظ/rollback المطلوبة تُحدد وتنفذ في الخطوة عند بدء التنفيذ.

المرجع التنفيذي: [ROADMAP](../planning/ROADMAP.md). لا اعتماد من مرحلة مبكرة على P08؛ التأسيس في الخطوات المبكرة والاستكمال في P08 يمنعان الدورة. لا نقل أو حذف أو إضافة معرفات Pxx-Syy.

## سجل مراجعة وإغلاق P00-S03

2026-10-01: المستخدم قال «راجعت تقرير P00-S03 وأعتمد توزيع التأسيس التالي ... ثبّت القرارات واقفل P00-S03 فقط». هذا اعتماد V4 للتسلسل بعد التصحيحات أعلاه، وإذن بإغلاق S03 وتسليم S04 Ready وتحديث CURRENT_STATE إلى P00-S04 NotStarted. لا يبدأ تنفيذ S04 أو P01، ولا Backend أو Commit أو Push. توجيه InReview السابق انتهى بهذا الاعتماد، ومنع Commit/Push مستمر في مهمة الإغلاق الحالية.

## سجل اعتماد P00-S04 وإغلاق P00

2026-10-04: المستخدم قال «أعتمد مخرجات P00-S04 بعد مراجعتها، وأسمح بإغلاق مرحلة P00 ودمج PR #1»، وحدد Squash Merge ثم تحديث main مع الاحتفاظ بفرع العمل. هذا اعتماد V4 للخطوة ومراجعة المرحلة وإذن Commit وPush والدمج دون force push أو تجاوز حماية الفرع. تسجل S04 Done ومراجعة P00 Approved وتسليم P01-S01 Ready والحالة P01-S01 NotStarted. لا تبدأ P01 أو تنشأ Solution أو Backend في مهمة الإغلاق. D001–D008 وQ003-01 إلى Q003-06 ثابتة؛ قواعد Git في GIT_WORKFLOW معتمدة. التفاصيل في [مراجعة P00](../planning/phase-00/PHASE_REVIEW.md).
