# سجل قرارات HawalekNow

آخر تحديث: 2026-11-25

## D021 إذن تنفيذ P04 بعد نجاح بوابة P03

Status: Approved
ApprovalDate: 2026-11-25
ExecutionMode: Phase
AuthorizedPhase: P04
PhaseUserApproval: Pending
MergeAuthorization: NotAuthorized
P05Execution: NotAuthorized
BaseMain: 63b189618a9c856151920da7669a4e2c9af86461
PredecessorAcceptance: D020

تحققت شروط إذن المستخدم في D020: أغلقت PR #10 بـSquash، وحدثت main المحلية بـff-only إلى SHA أعلاه وطابقت شجرتها رأس الإغلاق. نجحت فحوص ما بعد الدمج و370 اختبارًا بلا فشل أوتخطٍّ، وFormat وDocker والفاحصان وبوابات 46+27+41 وCI main45600216946 بالوظائف الثلاث. [إيصال P03](../planning/phase-03/MERGE_RECEIPT.md) و[الأدلة](../planning/phase-03/merge-verification.json) يثبتان البوابة، وتسليم P04 Ready.

بدأ الفرع codex/p04-implement-merchants-stores-and-branches من main المتحقق منها لتنفيذ S01–S07 بالترتيب بعد التحقق التقني المناسب لكل خطوة، دون انتظار اعتماد بشري لكل خطوة. هذا إذن تنفيذ وليس اعتماد مخرجات P04؛ تسلم المرحلة كاملة InReview في Draft PR واحدة، مع UserApproval/V4 Pending وتسليم P05 Draft. لا دمج P04 أوبدء P05. التجربة الشخصية وجلسة الشرح السابقة Deferred وليستا Passed؛ الشرح التفصيلي مهمة مستقلة بعد التسليم. تحفظ ملفات Word وSeed والجدول والفروع والبيانات وVolumes وسياسات Identity وLogging وحدود الوحدات وفق طلب المستخدم.

## D020 قبول P03 وإذن إغلاقها وبدء P04 بعد البوابة

Status: Approved
ApprovalDate: 2026-11-25
ReviewedHead: d6e9f7e4d65c891a4a932cccd02c88fe00c35057
AcceptanceBasis: CodeTestsCIAndDockerCorrection
ClosureAuthorization: Approved
MergeAuthorization: Approved
P04Execution: ConditionalOnP03Closure
P04MergeAuthorization: NotAuthorized
P05Execution: NotAuthorized
UserScalarReview: Deferred
UserPostmanReview: Deferred
EducationalWalkthrough: Deferred

اعتمد المستخدم P03 تقنيًا بناءً على مراجعة الكود والاختبارات وCI وتصحيح Docker، وأذن صراحة بـCommit وPush وإغلاق المرحلة وتحويل PR #10 إلى Ready ثم Squash Merge بعد نجاح فحوص الإغلاق وCI على رأس الإغلاق وفحص الحماية دون تجاوزها. الرأس المحلي والبعيد يطابقان الرأس المراجع، وCI45407402244 ناجحة بالوظائف الثلاث. تحفظ D018/D019 وسجلات Pending والأخفاقات السابقة كتاريخ؛ هذا القرار الأحدث يحل محل منع إغلاق P03 فقط. التجربة الشخصية وجلسة الشرح غير المكتملتين Deferred وليستا Passed؛ V4 Approved يعني قبولًا تقنيًا بهذا القرار دون ادعاء تنفيذ التجربة.

بدء P04-S01 إلى S07 مأذون بعد الدمج الفعلي وتحديث main بأمان ونجاح فحوص ما بعد الدمج وCI على main وتسليم P04 Ready. التنفيذ بالترتيب بعد التحقق التقني المناسب لكل خطوة. تسلم P04 كاملة InReview في Draft PR واحدة، واعتماد المستخدم وV4 Pending وتسليم P05 Draft؛ لا دمج P04 أو بدء P05. يثبت إذن التنفيذ عند نجاح البوابة بالمعرف التالي المتاح. تحفظ ملفات Word وSeed والجدول خارج التعديل وstaging والفروع والبيانات والVolumes؛ لا force push أو prune أو تغيير موارد مشاريع أخرى. عند فشل فحص مانع يتوقف الانتقال ولا تعتبر نتيجة تعذر العزل نجاحًا.

## D019 اعتماد مصدر Seed الجغرافية وحدود تغطيته

Status: Approved
ApprovalDate: 2026-11-21
OwnerPhase: P03
Source: https://github.com/open-admin-data/egypt-administrative-divisions
SourceCommit: 76b47b22872288c0d8b0c757975980e6986945e8
Coverage: 27 governorates;365 districts;5716 shiyakhas
Completeness: NotClaimed

اختار المستخدم صراحة «اعتماد النسخة بنطاقها ونواقصها الموثقة». تعتمد النسخة المفحوصة كنطاق Seed أولي، مع حفظ الأسماء والمعرّفات الأصلية دون اختراع تكملة أو إعادة تسمية الأقسام المختلفة التي تتشارك اسمًا إنجليزيًا. في النسخة 14 قسمًا/مركزًا بلا مناطق و18 مجموعة أسماء إنجليزية متكررة داخل المحافظة. لا ينسب لهذا القبول إثبات اكتمال بيانات مصر الحالية أو صحة كل إحداثية؛ النواقص موثقة في مراجعة المصدر. تعتمد مستويات المحافظة ثم القسم/المركز ثم الشياخة كتقسيم مرجعي أولي، وتوضح عقود الاختيار أن مستوى City يشمل القسم/المركز في المصدر.

القبول يحسم سؤال المصدر السابق فقط؛ لا يعتمد مخرجات P03 أو V4 ولا يجيز دمجها أو بدء P04. تستمر D018 وحدود التنفيذ الأصلية. ترخيص النسخة CC BY 4.0؛ يلزم حفظ النسب والترخيص ونسخة المصدر وبصماتها عند إدراج Seed.

## D018 إذن تنفيذ P03 بعد اكتمال بوابة P02

Status: Approved
ApprovalDate: 2026-11-21
ExecutionMode: Phase
AuthorizedPhase: P03
PhaseUserApproval: Pending
MergeAuthorization: NotAuthorized
P04Execution: NotAuthorized
BaseMain: 5e4250ff31f84c8806649c7ef1786574da7dd00a
PredecessorAcceptance: D017

طلب المستخدم الأصلي أذن بتنفيذ P03-S01 إلى S07 بعد إغلاق ودمج P02 والتحقق بعدها. تحققت البوابة: PR #9 merged=true على SHA أعلاه، وCI main44892733967 ناجحة بكل Jobs، وإعادة محلية بعد الدمج لـlocked Restore وRelease Build و270 اختبارًا وFormat والفاحصين و46+27 حالة بوابة وdiff نجحت. [تقرير الإغلاق الفعلي](../planning/phase-02/MERGE_RECEIPT.md) يسجل الروابط والحدود دون تغيير تاريخ السجلات السابقة.

الفرع codex/p03-implement-reference-data-and-locations بدأ من main المذكورة. هذا إذن تنفيذ وليس اعتماد مخرجات؛ P03 وV4 تبقيان Pending حتى المراجعة، وتسليم P04 يكون Draft، دون دمج P03 أو بدء P04. التنفيذ المتتابع والعقود والأمن والمعاملات وحدود النطاق في طلب المستخدم ملزمة. قبل بدء الكود يوجد سؤال مؤثر غير محسوم عن مصدر Seed الجغرافية ونطاق التغطية، موثق في [مراجعة المصدر](P03_SOURCE_REVIEW.md)؛ لا يفترض اعتماد المصدر أو أسماء بديلة قبل جواب المستخدم. لم تبدأ ملفات خطوات P03 أوكودها بعد.

## D017 قبول P02 وإذن إغلاقها وبدء P03 بعد البوابة

Status: Approved
ApprovalDate: 2026-11-21
ReviewedHead: d08e057e11516b25dca7fb22403a39a12b5b976b
AcceptanceBasis: CodeTestsCI
ClosureAuthorization: Approved
MergeAuthorization: Approved
P03Execution: ConditionalOnP02Closure
UserScalarReview: Deferred
UserPostmanReview: Deferred
EducationalWalkthrough: Deferred

قبل المستخدم مخرجات P02 بناءً على مراجعة الكود والاختبارات وCI، بشرط إعادة نجاح فحوص الإغلاق، وأذن بإغلاق PR #9 وتحويلها إلى Ready ثم Squash Merge بعد فحص الحماية وقابلية الدمج. تجربته الشخصية وجلسة الشرح غير المثبت اكتمالهما مؤجلتان بهذا القرار، وليستا Passed. تاريخ القبول الجديد هو 2026-11-21؛ D015 وسجلات Pending السابقة تحفظ باعتبارها تاريخًا قبل هذا القبول.

بعد نجاح الإغلاق والدمج وفحوص ما بعد الدمج وتسليم P03 Ready فقط، يأذن المستخدم بتنفيذ P03-S01 إلى S07 على مستوى المرحلة ثم تسليمها InReview في Draft PR واحدة؛ اعتماد مخرجات P03 وV4 يبقيان Pending، دون دمجها أو بدء P04. يسجل إذن التنفيذ عند تحقق بوابة البداية بالمعرف التالي المتاح. لا يغير هذا القرار سياسات P02 أو حدود الوحدات أو جدول المستخدم أو ملفات Word.

عند فشل فحص مانع قبل الدمج أو بعده يتوقف المسار ولا تبدأ P03. [محاولة الإغلاق الحالية](../planning/phase-02/CLOSURE_ATTEMPT.md) تسجل النتائج الفعلية؛ هذا القرار لا يثبت نجاح الإغلاق أو CI على رأس إغلاق جديد ولا الدمج أو جاهزية التسليم.

## D016 منع الحذف الذاتي للـAdmin

Status: Approved
ApprovalDate: 2026-11-10
OwnerStep: P02-S09

بعد شرح أثر حذف آخر حساب إدارة، اختار المستخدم صراحة «منع الحذف الذاتي للـAdmin». الحذف الذاتي والاسترجاع ضمن P02 يخصان Customer وMerchant فقط؛ Admin لا يستطيع استخدام مسار حذف حسابه. لا تُنشأ صلاحية حذف Admin أو تجاوز لحساب الإدارة ضمن هذه المهمة. هذا لا يمنع إدارة الجلسات أو استرجاع كلمة المرور للـAdmin، ولا يضيف أدوارًا أو صلاحيات موظفين.

## D015 إذن تنفيذ P02 على مستوى المرحلة

Status: Approved
ApprovalDate: 2026-11-10
ExecutionMode: Phase
AuthorizedPhase: P02
PhaseUserApproval: Pending
MergeAuthorization: NotAuthorized
P03Execution: NotAuthorized

الملف المرفق في هذه المهمة أذن بتنفيذ P02-S01 إلى S09 بالتتابع بعد ثبوت إغلاق واعتماد P01 ودمج PR #8 وHandoff Ready. تحققت البوابة فعليًا في 2026-11-10؛ PR #8 merged_at=2026-11-10T11:01:00Z وmain المحدثة b7a502051ac52f06b57c541e24f4bdde270a3392. هذا إذن تنفيذ فقط؛ اعتماد P02 وV4 يبقيان Pending، وتسليم P03 Draft، دون دمج أو P03. تفاصيل الشروط كاملة في طلب المستخدم والتصميم؛ لا يغير هذا D013 أو يفترض مراجعات بشرية جديدة.

## D014 تفرد الحسابات وحجز المعرّفات بعد الحذف

Status: Approved
ApprovalDate: 2026-11-10
OwnerStep: P02-S01

جواب المستخدم الصريح في مهمة P02: Email وUsername «فريدان داخل كل AccountType»، وبعد الحذف النهائي «لا، يظلان محجوزين». يسمح ذلك باستخدام البريد واسم المستخدم نفسيهما لحساب عميل وحساب تاجر مستقلين، دون مشاركة UserId أو Sessions أو تغيير نوع الحساب. يلزم تحديد AccountType في الدخول والاسترجاع لتجنب الالتباس. هذا التوجيه الأحدث يعدل عبارة البريدين المختلفين في قسم 2 من مرجع المتطلبات، ولا يدمج الحسابين أو يغير D005/D010.

الحجز يستمر أثناء مهلة 30 يومًا وبعد الحذف النهائي؛ التصميم يحفظ HMAC للمعرّفات المطبعّة مع فصل النوع والغرض، دون الاحتفاظ ببيانات الدخول الأصلية في سجل الحجز. مفتاح الحجز سر دائم خارج Git. تفاصيل الحماية والمدد غير المحددة في المراجع توثق كاختيارات تقنية، ولا تنسب إلى اعتماد المستخدم. [تصميم P02](../planning/phase-02/DESIGN.md).

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

هذا سجل الأسلوب السابق؛ D012 أدناه يحل محل بوابة اعتماد كل Step للأعمال الجديدة، دون تغيير الاعتمادات التاريخية.

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

## D009 تأجيل Docker وإغلاق P01-S01

Status: Approved
ApprovalDate: 2026-10-05
VerificationStep: P01-S06

حالة التنفيذ الجارية 2026-10-21: نُفّذ التأجيل بإذن S06 المستقل، ولا يمثل هذا تغيير اختيار SQL Server أو المعمارية أو إعداد الجهاز.

EngineVerification: Passed
ComposeVerification: Passed
SqlVerification: Passed
FullTextVerification: Passed
MailpitVerification: Passed
HealthVerification: Passed
PersistenceVerification: Passed

Server 29.7.2 وCompose 5.4.0 وLinux x86_64، 12 CPU و3910356992 bytes RAM. SQL 17.0.5015.4 وحزمتا المحرك وFTS 17.0.5015.4-1؛ اختبار فهرس فعلي وCONTAINS أعاد النتيجة المتوقعة. Mailpit v1.31.6 استقبل رسالة SMTP غير حساسة وأكدتها API. الخدمتان Healthy وآخر خمس Exit Codes لكل منهما 0؛ الصف ونتيجة البحث والبريد استمرت بعد restart وإعادة إنشاء Containers مع نفس Volumes. لا تشمل النتائج تخزين التطبيق أو بحث الأعمال أو OTP أو Production. [S06](../planning/phase-01/steps/S06.md) يوثق الصور والمصادر والفشل الأول وإصلاح السكربت وإعادة التحقق الفعلية؛ لا تمحى سجلات التأجيل أدناه.

السجل التالي تاريخ اعتماد تأجيل S01، محفوظ كما حدث ولا يمثل نتيجة فحص S06 الجارية:

المستخدم اعتمد تأجيل تشغيل وفحص Docker Engine وLinux containers إلى P01-S06. فحصه Deferred وليس Passed؛ لا يمنع إنشاء مشاريع .NET والاختبارات المحلية. اختبارات Containers وSQL تنتظر جاهزية الخدمات.
اعتمد المستخدم إغلاق P01-S01 بعد المراجعة والدمج، ثم وجّه صراحة «لا متعملش p01 s02 خلص القبلها و قولي الرسالة الهبعتها لل agent». نطاق هذه المهمة إغلاق S01 ودمج PR #2 وتسليم رسالة تنفيذ S02، دون بدء S02 أو إنشاء مشاريعها.

## D010 ملكية Identity وMerchants

Status: Approved
ApprovalDate: 2026-10-11
OwnerStep: P01-S03

اعتمد المستخدم: Identity يملك حساب الدخول وEmail/Password وOTP والجلسات. Merchants يملك MerchantAccount وحالات اعتماد التاجر. موضع تنفيذ المتطلب في P02 لا يغير ملكيته داخل Merchants؛ P02-S01 يؤسس النموذج المملوك لها وP04-S01 يستكمل حالاته.
تأكيد البريد مستقل عن موافقة Admin: التاجر المؤكد وPending يقدر يدخل ويجهز بياناته؛ متجره وفروعه ومنتجاته لا تظهر للجمهور قبل Approved. لا ينفذ هذا السلوك في S03؛ توثق الملكية فقط، وتختبر القواعد وقت تنفيذ حالات الاستخدام.
مرجع التنفيذ: [P01-S03](../planning/phase-01/steps/S03.md) و[حدود الوحدات](../backend/MODULES.md). يوضح D005 ولا يغير اختيار Identity أو معمارية D001.

## D011 الوحدات الإضافية وملكية عمليات Admin

Status: Approved
ApprovalDate: 2026-10-11
OwnerStep: P01-S03

اعتمد المستخدم Favorites وReports وNotifications وAnalytics كوحدات مستقلة بجانب Identity وMerchants وCatalog وLocations وSearch المسماة في خطة P01-S03.
عمليات Admin تبقى داخل الوحدة صاحبة البيانات؛ لا Admin Module يجمع منطق الأعمال. Admin صلاحية تدار ضمن Identity. لا تنشأ وظائف هذه الوحدات أو صلاحياتها في S03.
مرجع التنفيذ: [P01-S03](../planning/phase-01/steps/S03.md) و[حدود الوحدات](../backend/MODULES.md). D001–D009 وQ003-01 إلى Q003-06 وتوقيت خطوات التأسيس محفوظة.

## D012 التنفيذ والمراجعة على مستوى المرحلة

Status: Approved
ApprovalDate: 2026-11-09
EffectiveFrom: P01-S07
ExecutionMode: Phase

أذن المستخدم بتنفيذ المتبقي من P01 في مهمة واحدة، ثم تسليم المرحلة كاملة للمراجعة والشرح قبل اعتمادها ودمجها. هذا اعتماد لطريقة العمل وإذن تنفيذ P01 فقط، وليس UserApproval للمرحلة أو V4 Passed أو إذن دمج أو بدء P02.

تبقى معرفات الخطوات الـ76 وترتيبها واعتماداتها ومخرجاتها وDefinition of Done وطبقات التحقق الأصلية. تنفذ الخطوات بالتتابع، ويجوز التقدم داخل المرحلة بعد اكتمال التنفيذ ونجاح التحقق التقني المطلوب. ExecutionStatus: Verified يصف هذه الجاهزية؛ Status: InReview وUserApproval: Pending وReviewStatus: Pending وV4: Pending تبقى حتى المراجعة البشرية الفعلية. Status: InternalReady في تسليم داخل المرحلة إذن انتقال تقني تحت إذن المرحلة، ولا يساوي اعتماد المستخدم أو إغلاق المرحلة.

تجمع مراجعات Scalar/Postman المؤجلة من S07 في PHASE_REVIEW لـP01. الشرح التفصيلي للكود والتجربة التعليمية سيكون في مهمة تالية مخصصة بعد تسليم التنفيذ؛ التقرير لا يعوضه. تبقى عناوين القوالب ثابتة لحفظ السجلات القديمة، وتضاف حقول التنفيذ والتحقق ومراجعة المرحلة للأعمال الجديدة.

يمنع إغلاق المرحلة أو تسليم المرحلة التالية Ready أو بدء أي ملف تنفيذ لها قبل نجاح المتطلبات واعتماد المستخدم الصريح وإذن بدء التالية. يبقى P01 InReview وHandoff إلى P02 Draft في مهمة التنفيذ الحالية. تتوقف الخطوة عند فشل تقني مانع أو قرار مؤثر غير محسوم أو صلاحية جديدة؛ بوابة Step القديمة وحدها لا تمنع التقدم المصرح به. لا يحول نجاح CI إلى اعتماد بشري ولا يحول فحصًا فاشلًا أو متعذرًا إلى Passed.

يجمع فرع وDraft PR واحدان المتبقي من المرحلة؛ PR #8 والفرع الموجود يعاد استخدامهما مع حفظ التاريخ والفروع، دون force push أو دمج. الاعتمادات التاريخية لـP00 وP01-S01–S06 محفوظة كما حدثت. D001–D011 وQ003-01 إلى Q003-06 وSDK وMediatR والمعمارية وسياسة Logging دون تغيير.

## D013 قبول P01 وإذن إغلاقها المشروط

Status: Approved
ApprovalDate: 2026-11-10
ReviewedHead: 4b4d5a574dd459eda0e091311eab908439cb2fd0
AcceptanceBasis: CodeTestsCI
ClosureAuthorization: Approved
MergeAuthorization: Approved
P02Execution: NotStarted
UserScalarReview: Deferred
UserPostmanReview: Deferred
EducationalWalkthrough: Deferred

2026-11-10: اعتمد المستخدم المخرجات التقنية لـP01 اعتمادًا مشروطًا بإعادة نجاح فحوص الإغلاق، وأذن بإغلاق المرحلة وCommit/Push وReady for Review وSquash Merge لـPR #8 وتجهيز تسليم P02 فقط. أساس القبول مراجعة الكود والاختبارات وأدلة CI عند الرأس 4b4d5a574dd459eda0e091311eab908439cb2fd0، وليس ادعاء تنفيذ المستخدم التجارب. تجربته الشخصية لـScalar/Postman وجلسة الشرح الكاملة غير مؤكدة الاكتمال؛ Deferred عن بوابة الإغلاق بهذا القرار، وليست Passed. تعليماتها محفوظة في backend/API_EXPLORATION.md وملفات backend/postman والدليل المحلي tmp/P01-CODE-WALKTHROUGH-AR.md. P02 NotStarted ولا إذن تنفيذها أو تشغيل SQL/Mailpit أو تغيير بياناتهما. تاريخ الاعتماد الجديد 2026-11-10؛ الاعتمادات والأخفاقات التاريخية محفوظة. إذا فشل فحص تقني يتوقف الإغلاق؛ لا تجاوز حماية أو تغيير إنتاج/حزم/SDK/معمارية/Logging أو جدول المستخدم.

يوضح هذا القرار تطبيق D012 بعد القبول البشري الفعلي؛ انتهاء منع الإغلاق والدمج السابق يخص P01 فقط. Handoff Ready يجهز تسليم P02 ولا يساوي إذن بدء تنفيذها. لا تخفيف لأي فحص تقني أو تحويل نتيجة Failed/Unavailable إلى Passed.
