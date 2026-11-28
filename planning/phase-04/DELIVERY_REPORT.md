# تقرير تسليم P04 وإيصال إغلاق P03

Updated: 2026-11-28
Status: InReview
UserApproval: Pending
V4: Pending
PersonalTrial: Deferred
EducationalWalkthrough: Deferred
P05Handoff: Draft

P04 كاملة InReview وفق D021 على الفرع `codex/p04-implement-merchants-stores-and-branches` في [Draft PR #11](https://github.com/mohamedfathy5242420-cloud/HawalekNow/pull/11). S01–S07 Verified تقنيًا مع597 اختبارًا وCI وDocker ناجحة على رأس التنفيذ. اعتماد المستخدم وV4 Pending، وتسليم P05 Draft. لا دمج P04 أو بدء P05، والشرح التعليمي التفصيلي مهمة مستقلة.

## إيصال P03

أُغلقت P03 وفق قبول المستخدم D020. رأس الإغلاق هو `e15ee40971a384dd364094308d80499854e89907`، ودُمجت [PR #10](https://github.com/mohamedfathy5242420-cloud/HawalekNow/pull/10) بـSquash في `2026-11-25T10:59:43Z` إلى main عند `63b189618a9c856151920da7669a4e2c9af86461`. احتُفظ بالفرع محليًا وبعيدًا.

نجحت [CI الإغلاق 45599519138](https://github.com/mohamedfathy5242420-cloud/HawalekNow/actions/runs/45599519138) على رأس الإغلاق نفسه، ثم [CI main 45600216946](https://github.com/mohamedfathy5242420-cloud/HawalekNow/actions/runs/45600216946) على SHA الدمج. نجحت فحوص ما بعد الدمج: locked restore، وبناء Release بلا تحذيرات أو أخطاء، و370 اختبارًا (47 Architecture +247 Integration +76 Unit)، صفر فشل أو تخطٍّ، وFormat والتوثيق وGit وبوابات الرفض وdiff وDocker. تحققت موارد Seed الثلاث وملفا النسب والترخيص وبصماتها؛ طابق Docker 157 مدخلًا مطلوبًا و79 عينة ممنوعة و104 ملفات نشر نظيفة.

يحفظ [الإيصال](../phase-03/MERGE_RECEIPT.md) و[الدليل المنظم](../phase-03/merge-verification.json) و[الحالة التاريخية](../../docs/P03_CLOSED_STATE.md) أدلة الدمج والفحوص والإخفاقات السابقة. أصبحت بوابة P04 وتسليمها Ready قبل إنشاء فرعها من main المتحقق منها. التجربة الشخصية والشرح Deferred وليسا Passed.

## ما نُفذ في P04

| الخطوة | المخرج الفعلي | دليل التنفيذ |
| --- | --- | --- |
| S01 | حالات Pending وChangesRequested وApproved وRejected وSuspended وانتقالاتها؛ تجهيز بيانات المعلق ومنع النشر قبل الاعتماد. Identity تملك البريد والدخول والجلسات | [S01](steps/S01.md)، دون Migration جديدة للحالات الموجودة |
| S02 | الموافقة وطلب التعديل والرفض والتعليق ورفعه وإعادة إرسال المالك؛ صلاحيات حية وأسباب ونسخ، وAudit فعلي في معاملة القرار نفسها | [S02](steps/S02.md)، SQL/HTTP وrollback بعد Save وتزامن؛ لا شروط مستندات تجارية |
| S03 | متجر واحد للحساب، نشاط رئيسي واحد، اسم واحد، Phone أوWhatsApp على الأقل؛ لوجو وروابط اجتماعية اختيارية، وتخزين محلي آمن | [S03](steps/S03.md)، unique/FK وملكية ومحتوى ملفات وصلاحيات واستمرار وتعويض وتنظيف |
| S04 | فروع وPickupPoints متعددة، عنوان عام ثابت بإقرار، مراجع إدارية متسقة، geography SRID4326 بمحوري X=Longitude وY=Latitude، وfallback مستقل لكل قناة | [S04](steps/S04.md)، SQL للمحاور وحماية الحذف وإعادة الإسناد والتزامن |
| S05 | جدول أسبوعي إلزامي لكل مكان؛ أيام مغلقة وفترات تعبر منتصف الليل، وحساب openNow/nextOpen/closesAt وفق Africa/Cairo وTimeProvider | [S05](steps/S05.md)، حدود زمنية وDST ومنع التداخل؛ المكان المغلق يظل ظاهرًا |
| S06 | طلب نشاط معلق واحد، قبول أو رفض إداري؛ يبقى النشاط القديم أثناء الانتظار والرفض، ويتغير مع القرار وAudit ذريًا عند القبول | [S06](steps/S06.md)، نسختا الطلب والمتجر وFK وتزامن وrollback، دون تنفيذ منتجات P05 |
| S07 | قراءات وتعديلات Admin للمتجر والفروع واللوجو، وإخفاء/نشر وتعليق/رفع تعليق بعلامتين مستقلتين، مع السبب وAudit والنسخ والصلاحيات الحية | [S07](steps/S07.md)، جميع اختبارات الحل597 ناجحة، بما فيها7حالات SQL و7HTTP للإدارة ورحلة SMTP فعلية |

الإخفاء يمنع كل قراءات المتجر واللوجو والأماكن العامة ويتيح تجهيز المالك. تعليق Store يمنع الظهور وتعديلات المالك التجارية، ويتيح قراءة السبب الخاصة. Resume لا يمحو Hide، وPublish لا يمحو Suspend ولا يوافق على تاجر Pending. تعليق حساب التاجر عبر S02 يحافظ على منع الدخول والجلسات المؤسس في Identity. الفروع الجديدة للمعتمد لا تحتاج مراجعة مستقلة، والإغلاق الزمني لا يخفيها.

## الملفات والحزم وMigrations

| المجموعة | الملفات الرئيسية والغرض |
| --- | --- |
| Domain | `Modules/Merchants/MerchantAccount.cs` و`Store.cs` و`MerchantBranch.cs` و`WeeklyHours.cs` و`StoreActivityChange.cs`؛ حالات وقواعد أعمال، مع GeoPoint وAudit في Shared |
| Application | عقود وطلبات Merchants وValidation وMediatR وResult، ومنها `StoreAdministrationRequests.cs`؛ عقود الهوية والأنشطة والتقسيم الإداري و`IFileStorage` دون Entity عابر للوحدات |
| Infrastructure | `MerchantReviewService.cs` و`StoreService.cs` و`BranchService.cs` و`ActivityChangeService.cs` و`StoreAdministration.cs` و`MerchantStoreAccess.cs`؛ معاملات وSQL وملكية وأقفال؛ `Shared/Storage` و`LogoCleanup.cs` للتخزين والتنظيف |
| API | Controllers مراجعة التاجر والمتجر والفروع وتغيير النشاط و`StoreAdministrationController.cs`، تحت سياسة الأخطاء والأمان القائمة |
| التحقق والتشغيل | اختبارات Unit/Architecture/Integration، و`backend/test-p02.ps1`، و`docker/test-store-files.ps1`، ودليل التشغيل وPostman وملفات الخطوات والتسليم والأدلة |

تحدد [ملكية الوحدات](../../backend/MODULES.md) و[دليل Merchants](../../backend/MERCHANT_OPERATIONS.md) العقود والملفات وحدودها. يظهر الفرق الكامل في PR؛ يظل Controllers → MediatR → حالة الاستخدام → DbContext/UoW هو المسار المعتمد.

| Migration إضافية | الغرض |
| --- | --- |
| `20261125114145_P04MerchantReviewsAndAudit` | المراجعات وoperations.AuditEntries |
| `20261126141141_P04StoresAndLogoStorage` | Stores وقيودها ومرجع النشاط واللوجو |
| `20261126152640_P04BranchesAndAdministrativeReferences` | الأماكن وgeography والمرجع الإداري المركب |
| `20261126155950_P04BranchWeeklyHours` | ساعات JSON إلزامية؛ backfill مغلق لبيانات S04 دون default دائم |
| `20261126162152_P04StoreActivityChanges` | الطلبات وتاريخها ونسخها وطلب معلق واحد وFK الأنشطة |
| `20261127171325_P04StoreModeration` | علامتا Store وأسبابها وقيد SQL |

وُلدت Migrations بإعداد DesignOnly غير متصل، وطُبقت في الاختبارات على قواعد عشوائية جديدة يملكها fixture. لم تُطبق على قاعدة التطبيق.

الحزمة المباشرة الجديدة الوحيدة هي Magick.NET-Q8-AnyCPU `[15.1.0]`، ومعها Magick.NET.Core 15.1.0 انتقالية، بترخيص Apache-2.0. لا SDK أوحزمة قائمة رُقّيت. تحفظ [مراجعة الاعتماد](../../backend/P04_DEPENDENCIES.md) المصادر الرسمية والإصدار والتراخيص والبصمات؛ يُنشر LICENSE وNOTICE فعلًا. فحص NuGet الانتقالي في 27 نوفمبر لم يبلغ عن حزم معرضة من المصادر المتاحة حينها؛ هذا بيان عن الفحص وتاريخه.

## endpoints والصلاحيات

جميع المسارات تحت `/api/v1`. يسرد [دليل HTTP الكامل](../../backend/MERCHANT_OPERATIONS.md) كل method/path ومدخلاته وحدوده واستجاباته.

| المسارات | الصلاحية والأثر |
| --- | --- |
| `admin/merchants` وقراراته وAudit | AdminOnly حي؛ أسباب مطلوبة للقرارات عدا الموافقة |
| `merchants/review` وإعادة الإرسال | جلسة التاجر الحالي وملكية SQL وحالة تسمح بالفعل |
| `merchants/store` و`merchants/stores/{id}` واللوجو | VerifiedMerchantSetup وملكية حالية؛ تعليق Store يمنع التعديل |
| `merchants/stores/{storeId}/branches` وتعديل الفرع | السياسة والملكية نفسهما؛ مراجع ومواعيد إلزامية |
| `merchants/stores/{storeId}/activity-change-requests` | المالك الحالي؛ لا طلب أثناء تعليق Store |
| `admin/store-activity-changes` وapprove/reject | AdminOnly، وIdentity المالك الحالية، ونسختا الطلب والمتجر |
| `admin/stores` وتفاصيلها وhide/publish/suspend/resume واللوجو والفروع وAudit | AdminOnly حي؛ كل تعديل بسبب ونسخة، دون إعادة تعيين الملكية أو النشاط في جسم التفاصيل |
| `admin/merchant-branches/{id}/audit` | AdminOnly؛ تاريخ محفوظ بعد purge |
| `stores/{id}` ولوجوه و`stores/{storeId}/branches` و`branches/{id}` | Anonymous عند اعتماد التاجر وIdentity المتاحة وStore غير مخفي أو معلق؛ لا مفاتيح تخزين أو أسباب إدارية |

تستخدم mutations HTTPS وCookie الحالية وCSRF المرتبطة بالفاعل وحد العمليات القائم. يأتي actor من الجلسة، ولا يُقبل owner أوRole من Body. النسخة المتعارضة 409، والملكية الغائبة 404، ومنع التعديل 403، وفشل التخزين 503 آمنة دون مسارات أو تفاصيل استثناءات. يقرأ Admin metadata للحساب المحذوف مؤقتًا، لكن تعديلاته لا تتجاوز حذف Identity.

## المعاملات وAudit والأحداث والمهام

تملك كل حالة تغيير SQL معاملة UoW خارجية واحدة: قرار الحساب مع Audit، وتعديل Store/Branch مع Audit، وقبول النشاط مع Store والقرار وAudit. تُفحص الصلاحيات والنسخ والحالة الحالية تحت أقفال Identity وCatalog وLocations المملوكة لوحداتها. لا يجمع Orchestrator معاملات مستقلة لمجرد مرورها عبر MediatR؛ تشترك تعديلات Admin والمالك في النواة المالكة للمعاملة.

يسجل Audit الفاعل والعملية والهدف والسبب المناسب وUTC، مع قبل/بعد محدودين للحالات والأنشطة. لا يسجل صور Entities أوHTTP payload أوPasswords أوOTP أوTokens. هو منفصل عن Logging، وقراءته للإدارة فقط، ويظل محفوظًا بعد purge بلا FK حاذف إلى Identity. تستكمل P08 تغطيته العامة وفق Q003-03.

كتابة اللوجو تسبق مرجع SQL. يحفظ تغييره وAudit ونية الحذف المشفرة في معاملة SQL نفسها. يفحص التعويض المراجع المثبتة بقفل ومعاملة جديدة بعد انتهاء المعاملة الأصلية؛ فقدان ACK لا يبرر حذف ملف مرجعه Committed. يعمل consumer `Merchants.DeleteLogo.v1` بعد Commit، ويتحقق من الملكية وعدم وجود مرجع ويستعمل retry/failure القائمين. مهمة `merchant-logo-reconciliation-v1` ساعية على queue `storage` تصلح الأيتام المملوكة الأقدم من 24 ساعة. يحتفظ النظام بالملف المعدل أوغير مثبت الملكية بدل حذفه. في تعديل Admin يبقى مالك نية التنظيف هو التاجر، والفاعل في Audit هو Admin.

التخزين خارج webroot بمفاتيح داخلية، وPNG/JPEG فعلية محدودة إلى 2MiB و2048px وتطبيع PNG وإزالة metadata، مع منع traversal والروابط والملفات غير المملوكة وصلاحيات خاصة. أثبتت اختبارات S03 الاستمرار عبر عمليتين وحاويتين ورفض UID آخر؛ تُفحص بوابة Docker أيضًا لرأس التسليم النهائي، وتثبت نتيجتها الدقيقة في وصف PR.

لا حدث أوJob شكلي في S04–S07. قبل تمكين P05، يجب ضم تحويل المنتجات غير المتوافقة إلى Draft إلى معاملة قبول النشاط نفسها عبر عقد دورة المنتجات المملوك لـCatalog، وفحص أهلية Store/Branch الحالية للنشر. account publishing-access وحده ليس أهلية متجر أومنتج. لا منتجات أوصور منتجات أوبحث في P04.

## نتائج التحقق والإخفاقات المحفوظة

نجحت [CI تنفيذ S07، التشغيل46145684103](https://github.com/mohamedfathy5242420-cloud/HawalekNow/actions/runs/46145684103) على `0c88c6bba8792bd8b3f9dd1eb52d0ada942eb98e`: جميع الوظائف الثلاث completed/success، و597 اختبارًا (47Architecture+352Integration+198Unit)، صفر فشل أوتخطٍّ وبناء0/0 وFormat. أثبت Docker211مدخلًا مسموحًا و82عينة ممنوعة دون زائد أوناقص، و116ملف نشر وموارد Seed الثلاث وإشعارات الصور ببصماتها. أثبت الفحص المحلي على SHA نفسها وCI استمرار الملفات عبر عمليات وحاويات جديدة ورفض UID65534 مع حفظ الـvolumes. [إيصال Docker المحدد بالنطاق](docker-review-verification.json).

فشل أول تشغيلين كاملين لـS07 بمهلات توفير قواعد SQL؛ لم يُحسب أي منهما Passed. حُدد توازي المجموعات بأربع، ثم سُلسلت دورة إنشاء/ترحيل/حذف القواعد فقط، مع إبقاء الاختبارات وسباقات المعاملات والمهلات كما هي. أُوقف التشغيل التالي بطلب المستخدم قبل اكتماله، وحُفظ كدليل غير مكتمل. في28نوفمبر نجح locked restore وبناء Release بلا تحذيرات أوأخطاء وFormat، ثم انتهى تشغيل الحل الكامل بـexit0:47Architecture+352Integration+198Unit=597ناجحًا، صفر فشل أوتخطٍّ. نجحت بوابات الرفض46+27+43+19، وعينات Git27ممنوعة/36مسموحة، و8حالات رفض لعقد الأخطاء. اكتملت بوابات Docker وCI لرأس التنفيذ كما في الإيصال أعلاه.

تحفظ [الأدلة المنظمة](verification.json) وسجلات الخطوات الإخفاقات والتصحيحات، بما فيها CI S02 الأولى، وإصلاح decoder وصلاحيات التخزين، وتصحيح اختبار الترحيل التاريخي و51010 لقفل Catalog. لا إضعاف للفاحص أوإخفاء لحالة فشل.


إيصال التنفيذ أعلاه لا يُنسب إلى رأس أحدث. رأس Commit تحديث تقرير التسليم له CI وDocker مستقلتان؛ مرجع SHA ونتيجتهما الفعلية هو وصف [PR #11 وChecks الخاصة بها](https://github.com/mohamedfathy5242420-cloud/HawalekNow/pull/11) والتقرير النهائي للمهمة. لا ينتهي التسليم قبل التحقق منهما.

## التشغيل وPostman وخطوات التجربة

الأوامر الكاملة وإعدادات SQL والمفاتيح المستقلة وHTTPS ومجلدي المفاتيح والتخزين الخاصة في [دليل التشغيل](../../backend/MERCHANT_OPERATIONS.md#local-review-commands)، مع [دليل Identity](../../backend/IDENTITY_OPERATIONS.md) و[الدليل المرجعي](../../backend/REFERENCE_DATA_OPERATIONS.md). تُضبط الأسرار خارج Git. ينشئ مشغل الاختبارات قواعد معزولة ويستعمل SMTP/Mailpit المملوكين للمشروع؛ لا يهيئ قاعدة التطبيق تلقائيًا.

```powershell
./backend/start-p02-services.ps1
dotnet restore backend/HawalekNow.slnx --locked-mode
dotnet build backend/HawalekNow.slnx --configuration Release --no-restore --disable-build-servers -m:1
./backend/test-p02.ps1 -NoBuild -MaxParallelThreads 4
dotnet format backend/HawalekNow.slnx --verify-no-changes --no-restore
./planning/verify-roadmap.ps1
./planning/verify-git-workflow.ps1
```

استورد [P04 collection](../../backend/postman/HawalekNow.P04.postman_collection.json) و[بيئتها الفارغة](../../backend/postman/HawalekNow.P04.Local.postman_environment.json)، واستعمل P02 للتسجيل وتأكيد البريد والاسترجاع. اختر رحلة وقراراتها صراحة، وحدّث نسخ Store/Branch/الطلب بعد كل تغيير؛ لا تشغّل القرارات المتعارضة كلها آليًا.

1. سجّل تاجرًا وأكد بريده عبر البريد المحلي، ثم ادخل. جهز Store ولوجو وفرعًا أونقطة استلام بسبعة أيام ومراجع صالحة؛ تظل القراءة العامة 404 قبل الاعتماد.
2. بدّل إلى Admin: اطلب تعديلًا ثم أعد الإرسال كمالك، أووافق. تصبح البيانات عامة دون موافقة فرع منفصلة، حتى عندما يكون مغلقًا زمنيًا.
3. اطلب نشاطًا آخر كمالك؛ تظل القراءة العامة على القديم. حدّث النسختين كـAdmin واختر قبولًا أورفضًا، ثم راجع النشاط وAudit.
4. أخف المتجر وعلقه وجرّب قراءات الضيف وتعديلات المالك. ارفع كل علامة على حدة؛ اختبر تعديل Admin للبيانات واللوجو والفرع بالسبب والنسخة.
5. جرّب تاجرًا آخر وعميلًا وضيفًا وCSRF مفقودة ونسخة قديمة. جرّب دورة حذف Identity واسترجاعه، مع بقاء البيانات والعلامات وقواعد الجلسات.

تتحقق `MerchantPhaseJourneyTests` آليًا من التسجيل الفعلي وتوصيل SMTP وتأكيد البريد وتجهيز البيانات والموافقة والظهور والإخفاء وتغيير النشاط؛ لا يعني ذلك تنفيذ المستخدم تجربته الشخصية.

## حدود التسليم والحفظ

UserApproval وV4 Pending، والتجربة الشخصية والشرح Deferred. يبقى [تسليم P05](handoffs/S07-to-P05.md) Draft، ولا دمج P04 أوتنفيذ P05. Google Maps وFrontend وبريد الإنتاج ووظائف Admin الأوسع لإنشاء حسابات/منتجات بالنيابة وحذف تجاري مستقل ليست مخرجات هذه المرحلة.

ملفات Word وSeed وتراخيصها وبصماتها، والفروع والبيانات والـvolumes محفوظة. لم يُعدل أوstage ملف `docs/TARGET_SCHEDULE.md`؛ SHA256:
`26DC5102E591FA55D245C9294C3B2222B72DD6A57EA2E32AC29F999FDB5C2403`.
