# مراجعة P03 — البيانات المرجعية والمواقع

Updated: 2026-11-25
Status: Done
ExecutionAuthorization: D018
SeedDecision: D019
UserApproval: Approved
ReviewStatus: Approved
V4: Approved
P04Status: NotStarted
AcceptanceDecision: D020
ApprovalDate: 2026-11-25
ReviewedHead: d6e9f7e4d65c891a4a932cccd02c88fe00c35057
AcceptanceBasis: CodeTestsCIAndDockerCorrection
UserScalarReview: Deferred
UserPostmanReview: Deferred
EducationalWalkthrough: Deferred

الحالة الحالية Done/Approved وفق D020؛ التجربة الشخصية والشرح Deferred. الدمج وبوابة P04 قيد التحقق كما في [سجل الإغلاق](CLOSURE.md).

## سجل التنفيذ والمراجعة قبل القبول — تاريخي

P02 أغلقت ودمجت في PR9 عند 5e4250ff31f84c8806649c7ef1786574da7dd00a، ثم نجحت فحوص ما بعد الدمج؛ [إيصالها](../phase-02/MERGE_RECEIPT.md) يحفظ الأدلة وتأجيل التجربة الشخصية والشرح دون اعتبارهما Passed. تنفيذ P03 مأذون وفق D018؛ اعتماد المصدر D019 لا يعد اعتمادًا لمخرجات المرحلة.

الفرع codex/p03-implement-reference-data-and-locations و[Draft PR10](https://github.com/mohamedfathy5242420-cloud/HawalekNow/pull/10). S01–S07 Verified تقنيًا. نجح الحل كاملًا محليًا وعلى CI:370passed/0failed/0skipped؛ الفشل الأول 367/1 وسببمهلة Seed وإصلاحه محفوظان. لا دمج P03 أو بدء P04، و[التسليم](handoffs/S07-to-P04.md) Draft.

## السلوك والملكية

| الوحدة | العلاقات والقواعد |
| --- | --- |
| Catalog | Activity → Category بشجرة داخل النشاط نفسه، دون حد Business ثابت للعمق. فرادة الأسماء العربية والإنجليزية بين الأنشطة أو الأشقاء. FK مركبة تحمي الانتماء، وقفل SQL يحمي نقلين متزامنين من إنشاء دورة؛ RowVersion تحمي التحديث المفقود. |
| Locations | Governorate → City → Area؛ FK مركبة تمنع زوج محافظة/مدينة متعارضًا. نقل مدينة ذات مناطق يرفض 409؛ نقل الفارغة أو نقل منطقة إلى زوج صحيح ذري. الاسم الإنجليزي ليس هوية ولا يفرد قسرًا. |
| Search | SynonymGroup → SynonymTerm؛ النص الأصلي محفوظ، ومفتاح FormKC/فراغات/ToUpperInvariant فريد عبر جميع المجموعات حتى المعطلة. لا دمج لأحرف العربية المختلفة أو حذف للتشكيل والشرطات. أمثلة تيشرت/تي شيرت/T-shirt/Tshirt مصطلحات صريحة. |

تعطيل الأب يخفي نسله من الاختيار العام دون تعديل IsActive المخزنة للأبناء، وإعادة تفعيله تحترم تعطيل الابن المستقل. نقل التصنيف داخل نشاطه يغير مسار ظهوره، ولا يغير نشاطه. الحذف الذي يترك أبناء يرفض 409؛ لا ادعاء اختبار مراجع منتجات مستقبلية. حذف مجموعة مرادفات يحذف مصطلحاتها في المعاملة نفسها. كل CRUD متعدد التغييرات داخل UoW الحالية و DbContext واحدة، دون تجاوز ملكية وحدة أو مدخل عام لجداولها.

## خريطة HTTP الفعلية

جميع المسارات تحت /api/v1. الكتابة الإدارية تتطلب AdminOnly و HTTPS و CSRF، ومعدل 60 mutation/دقيقة لكل peer مشترك للبيانات المرجعية. الزائر 401 والمستخدم العادي 403؛ الإخفاقات بعقد ProblemDetails المغلق دون SQL أو أسرار. PUT/DELETE تحتاج RowVersion؛ DELETE تحملها في JSON body.

| المورد | العام | الإدارة |
| --- | --- | --- |
| الأنشطة | GET catalog/activities | GET/POST admin/catalog/activities؛ PUT/DELETE .../{id} |
| التصنيفات | GET catalog/categories?activityId&parentId | GET/POST admin/catalog/categories؛ PUT/DELETE .../{id} |
| المحافظات | GET locations/governorates | GET/POST admin/locations/governorates؛ PUT/DELETE .../{id} |
| المدن/الأقسام | GET locations/cities?governorateId | GET/POST admin/locations/cities؛ PUT/DELETE .../{id} |
| المناطق | GET locations/areas?governorateId&cityId | GET/POST admin/locations/areas؛ PUT/DELETE .../{id} |
| المرادفات | لا مسار عام أو بحث تجاري | GET/POST admin/search/synonym-groups؛ PUT/DELETE .../{id} |

الاختيار العام: language=ar/en، page من 1 إلى 100000 و pageSize من 1 إلى 100، وفرز ثابت بمعرف SQL وصفحة واحدة فقط. يعيد Id/Name دون RowVersion أو IsActive أو الاسمين معًا. الإدارة تعرض النسخة والحالة والأسماء، مع فلاتر الآباء للمواقع والتصنيفات. [دليل API](../../backend/API_EXPLORATION.md) و[Collection31 طلبًا](../../backend/postman/HawalekNow.P03.postman_collection.json) و[Environment فارغة الأسرار](../../backend/postman/HawalekNow.P03.Local.postman_environment.json). JSON/scripts وحارس CSRF فُحصت؛ لم تنفذ رحلة Postman كاملة أو مراجعة المستخدم الشخصية.

## Seed و geography و Cache

المصدر المقبول [Open Admin Data](https://github.com/open-admin-data/egypt-administrative-divisions) عند 76b47b22872288c0d8b0c757975980e6986945e8، بترخيص CC BY4.0 ونسب وبصمات محفوظة. النسخة تضم 27 محافظة و 365 قسمًا/مركزًا و 5716 شياخة؛ 14 قسمًا بلا مناطق و 18 مجموعة أسماء إنجليزية متكررة صحيحة. لا تكملة مخترعة أو ادعاء اكتمال مصر أو دقة إحداثياتها. إحداثيات المصدر لا تستورد. الأنشطة الأربعة من المتطلبات، والإنجليزية ترجمة عرض: Clothing and accessories؛ Electronics and mobile phones؛ Furniture and household goods؛ Car spare parts.

الأمر الصريح --seed-reference-data يضيف المفقود بمعاملة واحدة و Catalog ثم Locations، ويتحقق من المصدر قبل الكتابة. سجلات SourceKey دائمة تحفظ المعرفات وإعادة التشغيل حتى بعد حذف Admin؛ لا استبدال لأسمائه أو نقله أو تعطيله أو إعادة إحياء محذوفه. الفشل بعد حفظ الآباء يرجع الجميع. لا حسابات تنشأ أو HTTP/Jobs تبدأ في الأمر. [التشغيل الآمن](../../backend/REFERENCE_DATA_OPERATIONS.md) و[النسب والبصمات](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/SOURCE.md).

GeoPoint مستقلة عن EF، مع Latitude[-90,90] و Longitude[-180,180] finite. التحويل Infrastructure إلى Point4326 يستخدم X=Longitude/Y=Latitude. SQL أثبتت roundtrip ومحاورها ومسافة درجة على خط الاستواء نحو 111319.49 متر بسماحية 300 متر. المستهلك الإنتاجي المحدد في المراجع P04-S04؛ صحح التصميم افتراض centroid قبل الكود، ولم يضف كيانًا أو endpoint أو جدول Probe إنتاجيًا. [الحزم وتراخيصها](../../backend/P03_DEPENDENCIES.md): مزود NTS10.0.14 MIT، و NTS2.6.0 و SqlServerBytes2.1.0 BSD-3-Clause. لا تغيير SDK أو MediatR أو مخطط اعتماد الإنتاج.

[Cache العامة](../../backend/REFERENCE_CACHE.md) تستخدم IMemoryCache مستقلة لكل Host، و Snapshot immutable محددة الحقول. المفاتيح تشمل اللغة والمورد والآباء والصفحات وفلتر الفعال؛ TTL مطلق 60 ثانية وحد 8192 وحدة بتكلفة 1+عددالعناصر. إبطال بعد Commit فقط، و generation تحمي من إعادة نشر loader قديمة. لا تخزين حسابات أو صلاحيات أو Entities، ولا Redis أو اتساق موزع؛ Seed CLI قبل HTTP أو انتظار TTL للنسخ القائمة.

## Migrations والتحقق

أربع Migrations إضافية فقط: P03CatalogFoundation، P03LocationsHierarchy، P03ReferenceSeedKeys، P03SynonymGroups. لا Migration geography صورية؛ نوع وجدول التحقق داخل مشروع الاختبار وقاعدته المملوكة فقط. جميع الاختبارات تستخدم SQL فعلية وقواعد HN_P02_Test المملوكة للتشغيل، مع إبقاء خدمات المشاريع الأخرى وقاعدة التطبيق و Volumes دون حذف أو إعادة إنشاء.

النتيجة المجمعة الفعلية:47 Architecture+247 Integration+76 Unit=370passed، صفر failed/skipped، محليًا وعلى CI45004486033 عند 6fe86f60ca16987d9225747c065ec6ae2c99d732. نجح locked restore و Release Build صفر تحذيرات وأخطاء و Format والفاحصان و diff. المصدر والحزم وفروع الاختبار فعلية، والأعداد مستخرجة منسجلاتالتشغيل. [verification.json](verification.json) يحفظ نتائج كل خطوة، والفشل وإصلاحه بدل حذفه، ونتائج CI حسب رأسها الفعلي. فحوص الخطوات تثبت SQL constraints/ترقية/rollback/التزامن و HTTP الأمن والتفعيل والتصفح، و Unicode والإحداثيات و Cache. التحقق النهائي يشمل locked restore و Release Build و Tests كاملة و Format والفاحصين و diff وبوابات الرفض و CI الفعلية.

## حدود الاعتماد والتسليم

الخطوات و[التصميم](DESIGN.md) و[فهرس الملفات](FILES.md) و[تسليم P04](handoffs/S07-to-P04.md) للمراجعة. مراجعة المستخدم و V4 Pending، والشرح التعليمي والتجربة الشخصية مهمة منفصلة؛ لا تقرير أو CI يعوضهما. لا تنفيذ P04 أوموافقات تجار أو متاجر أو فروع أو منتجات أو بحث تجاري أو Frontend أو توسعة Audit/Logging. ملفات Word وجدول TARGET_SCHEDULE.md لم تتغير؛ الجدول خارج staging.

CI المسجلة في الأدلة تخص رأس التنفيذ 6fe86f6. نتيجة آخر رأس للتسليم موثقة في Checks ووصف Draft PR #10.

## تصحيح مراجعة Docker — 2026-11-24

إذن المستخدم الحالي يخص إصلاح الاستبعاد واختباراته وCommit/Push على نفس PR10 وCI، دون دمج أو اعتماد المرحلة. الرأس السابق للمراجعة11ffdecb0dded7ac71eb86c93f422024d0175289 مطابق محليًا وبعيدًا. جدول TARGET_SCHEDULE.md محفوظ خارج staging وببصمته السابقة.

اختبار Docker فعلي على نسخة نظيفة من ملفات Git وعلامات غير حساسة أثبت الفشل قبل الإصلاح: appsettings.Local.json دخلت عند جذر مشروعAPI وفي مستوىSeedData، ومرّت31 حالة محظورة من79، مع69 ملفًا زائدًا إجمالًا، وبقاء157 ملفًا مطلوبًا. السبب إعادة تضمين المجلدات الواسعة، والنمط appsettings.*.Local.json لا يطابق الاسم الأقصر. لا ادعاء تسريب سر حقيقي، ولا استخدام لإعدادات الجهاز الفعلية داخل الاختبار.

أصبحت .dockerignore تسمح بالملفات صراحة دون استثناء مجلدات كامل، مع استبعاد الاسم المحلي القصير والمتغيرات و.env وsecrets والمفاتيح والشهادات. الاستيراد مثبت بالأسماء للـSeedJSON الثلاثة وSOURCE.md وLICENSE.txt. الاختبار بعد التصحيح أثبت157 ملفًا مطلوبًا، وصفر ملفات محظورة من79 وصفر ملفات زائدة. فحص Linux يشمل الملفات المخفية.

نجح بناء صورةAPI وفحص مستقل داخلSDK المثبت وnetwork=none: ثلاثة مواردSeed داخلAssembly وبصماتها مطابقة، وملفا النسب والترخيص منشوران ببصماتهما، و104 ملفات نشر خالية من الأسماء المحظورة والعلامة الوهمية UTF-8/UTF-16. لم يبدأAPI أوSeed أواتصالSQL لإثبات ذلك. نجح locked Restore وRelease Build بصفر تحذيرات وأخطاء وFormat. التشغيل المحلي الجديد نجح فعلًا: 47 Architecture +247 Integration +76 Unit =370 passed، صفر failed/skipped، من ثلاثة ملفاتTRX. نجح فاحصا التوثيق وdiff؛ هوية SQL وMailpit ووقت تشغيلهما لم يتغيرا. نتيجة CI على رأس التصحيح المرفوع تُضاف حسب تشغيلها الفعلي إلى [أدلة التصحيح](docker-review-verification.json)، دون اعتبار CI السابقة دليلًا للرأس الجديد.

[تفصيل الاختبار والملفات المسموحة والمحظورة](../../docker/API_BUILD_CONTEXT.md). أضيف إلى وظيفةDocker الموجودة فيCI قبلsmoke المستقلة. بقاء الحالة InReview وUserApproval/V4 Pending وتسليمP04 Draft محفوظ؛ ملفاتSeed وترخيصها وبصماتها وSDK والحزم ووظائفP03 لم تتغير. السجل السابق وأدلته لم يُمسحا.

إيصال CI للتصحيح: [run45406019154](https://github.com/mohamedfathy5242420-cloud/HawalekNow/actions/runs/45406019154) completed/success على3bb51ee2df595eb0fc69b16e7f32d3d077d2faa3، والوظائف الثلاث success. السجلات تثبت370 اختبارًا (47+247+76) بلا فشل/تخطٍّ وBuild0/0، وDocker157/79 وثلاثة مواردSeed وملفي النسب والترخيص و104 ملفات نشر وsmoke ناجحة. نتيجة رأس إيصال الأدلة التوثيقي اللاحق تُراجع مستقلة فيChecks ووصفPR10؛ لا تُستنتج من نجاح هذا الرأس.


## قبول وإغلاق D020 — 2026-11-25

الحالة الملزمة هي Done/Approved وفق قبول المستخدم التقني. V4 Approved قبولٌ بهذا القرار، والتجربة الشخصية والشرح Deferred وليسا Passed. عبارات InReview/Pending ومنع الدمج في وصف التنفيذ السابق تاريخية، محفوظة دون تغيير الأدلة أو الأخفاقات. الدمج وبدء P04 مشروطان بفحوص الإغلاق وCI على رأسه والحماية والتحقق الفعلي بعد الدمج؛ [سجل الإغلاق](CLOSURE.md).
