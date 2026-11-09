# مراجعة P01 — الأساس الهندسي والبيئة

Phase: P01
Status: InReview
Updated: 2026-11-09
ExecutionMode: Phase
UserApproval: Pending
ReviewStatus: Pending
V4: Pending
UserScalarReview: Pending
UserPostmanReview: Pending
EducationalWalkthrough: Pending
NextStep: P02-S01

مراجعة مجمعة وفق D012، وليست اعتمادًا أو إذن دمج أو بدء P02. الشرح التفصيلي والتجربة التعليمية في مهمة تالية مخصصة؛ التقرير لا يعوضهما. [الحالة](../../docs/CURRENT_STATE.md)، [الأدلة](verification.json)، [S08](steps/S08.md)، [تسليم P02 Draft](handoffs/S08-to-P02.md).

## مطابقة الخطة وDefinition of Done

| المتطلب الأصلي | الدليل الحالي | الحالة |
| --- | --- | --- |
| S01 Monorepo/Solution وSDK | global.json وبنية المستودع و[S01](steps/S01.md) | Done واعتماد تاريخي |
| S02 Clean Architecture والاختبارات | المشاريع و[S02](steps/S02.md) | Done واعتماد تاريخي |
| S03 Modules وNamespaces | [MODULES](../../backend/MODULES.md) و[S03](steps/S03.md)، D010/D011 | Done واعتماد تاريخي |
| S04 CQRS وValidation/Logging/Performance وCorrelation | [S04](steps/S04.md) واختبارات Pipeline وعزل Logger | Done واعتماد تاريخي؛ إعادة فحص الحل |
| S05 Result وProblemDetails وGlobal Exception Handler | [S05](steps/S05.md)، Success(null)=200 JSON null وSuccess()=204 | Done واعتماد تاريخي؛ إعادة فحص الحل |
| S06 SQL Full Text/Mailpit/Volumes/Health والاستمرار | [S06](steps/S06.md) وD009 | Done؛ الدليل تاريخي، الموارد الآن متوقفة |
| S07 Scalar/OpenAPI وapi/v1 وHealth وPostman | [S07](steps/S07.md) و[التجربة](../../backend/API_EXPLORATION.md) | Verified تقنيًا؛ مراجعة المستخدم Pending |
| S08 Restore/Build/Tests/Format/Docker وCI على PR | [CI](../../backend/CI.md) و[S08](steps/S08.md) | Verified تقنيًا وInReview؛ PR CI ناجح، اعتماد المستخدم Pending |
| يعمل Solution محليًا وداخل Docker بأمر موثق | [Backend](../../backend/README.md) و[CI/Docker API](../../backend/CI.md) | Passed محليًا وDocker فعليًا وفي CI؛ API non-root/read-only بلا mounts |
| تفشل Architecture Tests عند كسر اتجاه الاعتماد عمدًا | DependencyTests.Isolated_mutation_is_rejected_without_editing_the_real_project وModuleBoundaryTests | 47 Architecture Passed ضمن baseline؛ حقن الاعتماد اختبار فعلي |
| يعرض Scalar وثيقة صحيحة وتعمل Health Checks | Program وApiFoundationTests وتجربة المتصفح | Scalar حملت JSON في 2026-11-09 وHealth API Passed؛ SQL/Mailpit متوقفتان وأدلتهما السابقة تاريخية |
| PR تجريبي يمر عبر GitHub Actions | Draft PR #8 وWorkflow | Passed؛ Run 42587912225 والـJobs الثلاث success |
| هيكل المشاريع والحزم والتراخيص وbootstrap والتنظيف الآمن | [PACKAGES](../../backend/PACKAGES.md) و[MODULES](../../backend/MODULES.md) و[Docker](../../docker/README.md) | مراجعة تقنية مكتملة؛ 56 زوج NuGet: 40 MIT و16 Apache-2.0؛ commands وCompose Validate/bootstrap rejection والصور متحققة، مراجعة المستخدم Pending |
| تسليم P02 ببيئة مستقرة قابلة للاختبار | [Draft handoff](handoffs/S08-to-P02.md) | Draft؛ لا تنفيذ P02 |

## مراجعات المستخدم المطلوبة

1. تجربة [Scalar](http://127.0.0.1:5080/scalar/) وتحميل المستند وإرسال فحص الحياة مع Correlation؛ التوثيق في Production يرد 404. تعليمات [API_EXPLORATION](../../backend/API_EXPLORATION.md).
2. استيراد Collection وEnvironment من backend/postman وتشغيل Development وProduction، وتأكيد حدود Liveness دون Readiness SQL/بريد.
3. مراجعة المشاريع والحزم والتراخيص وCI وصور Docker وحدود Logging والأدلة والقيود؛ جلسة شرح تفصيلية مستقلة بعد التسليم.
4. اعتماد P01 والدمج وبدء P02 قرارات لاحقة صريحة؛ لا تستنتج من نجاح الأدوات أو CI.

## القيود والأسئلة

لا Identity أو Persistence أو OTP أو Business أو Frontend. لا Production readiness أو نشر أو Registry. المعمارية static لا تغطي reflection/dynamic. Logging مقتصر على Pipeline؛ HostLogsBlocked وOperationalDiagnosticsIncomplete. صحة SQL/Mailpit الحالية غير متاحة لأن الموارد متوقفة؛ لا إعادة تشغيل ضمن المراجعة. قبل P02 يلزم تشغيلها عند الإذن بالأوامر الموثقة دون حذف Volumes. استمرار البيانات مثبت تاريخيًا في S06 فقط؛ بناء الصورة لا يعيد اختباره. جدول TARGET_SCHEDULE.md محفوظ بلا تعديل أو Commit.

## Git وأدلة التشغيل

Branch: codex/p01-s07-document-and-version-api
PR: [Draft #8](https://github.com/mohamedfathy5242420-cloud/HawalekNow/pull/8)

رأس البداية c29bde521e80ef13492c2c89265ebb4a41cf6d72. Run ID وSHA وJobs الجديدة تسجل بعد حدوثها، والفشل والإعادة في S08. لا دمج أو تغيير حماية الفروع.

دليل التنفيذ الناجح: [Run 42587912225](https://github.com/mohamedfathy5242420-cloud/HawalekNow/actions/runs/42587912225)، head=d9ab9199d346d733b35c20b171ee564dfbda5003، event=pull_request وcompleted/success. Jobs: Documentation and Git rules=success، Restore, build, tests and format=success، API and SQL image builds=success. Linux logs أكدّت 47 Architecture +72 Integration +7 Unit=126 passed، صفر failed/skipped، وBuild صفر warnings/errors. لا أعطال اختبارات مخفية أو تخفيف Assertions/التوازي؛ ملفات اختبارات C# لم تتغير. Format صحح whitespace فقط في ملفين. 23 حالة بوابة و8 عقود smoke خاطئة رُفضت. أول Run فشل قبل Jobs ثم صُحح runner context؛ smoke المحلي الأول كشف خطأ byte[] في أداة الفحص وصُحح مع regression. كل الفشل والإصلاح موثق في S08.

التشغيل المحلي Release والتحقق من Format والتوثيق وGit وDocker API/SQL نجحت؛ SQL build المحلي cached، وبناء CI في runner مستقل نجح. Scalar عرضت OpenAPI 3.1.1 والمسار والعقود فعليًا؛ Postman 10 طلبات/66 assertion دليل S07 التاريخي، وتجربتك الحالية Pending. فحوص Compose وbootstrap الآمنة نجحت دون تعديل .env أو تشغيل خدمات. بقيت الحاويتان السابقتان SQL=8fc79ef56ff4 وMailpit=6b5e4fad2acf متوقفتين وVolumes موجودة؛ SQL exit 137 سببه غير محسوم ولا يُفسر كفقد بيانات. لا يبرر هذا حذفًا أو إعادة إنشاء. قائمة الملفات المتغيرة وأسماء commits من Git diff/history، والـhead النهائي وCI الخاص به في تقرير التسليم؛ لا تختلق الوثيقة مرجعًا لنفس commit قبل إنشائه.
