# مراجعة P03 — البيانات المرجعية والمواقع

Updated: 2026-11-21
Status: InProgress
ExecutionAuthorization: D018
SeedDecision: D019
UserApproval: Pending
ReviewStatus: Pending
V4: Pending
P04Status: NotStarted

P02 أغلقت ودمجت في PR9 عند5e4250ff31f84c8806649c7ef1786574da7dd00a، ثم نجحت فحوص ما بعد الدمج؛ [إيصالها](../phase-02/MERGE_RECEIPT.md) يحفظ الأدلة وتأجيل التجربة الشخصية والشرح دون اعتبارهما Passed. تنفيذ P03 مأذون وفق D018؛ اعتماد المصدر D019 لا يعد اعتمادًا لمخرجات المرحلة.

الفرع codex/p03-implement-reference-data-and-locations و[Draft PR10](https://github.com/mohamedfathy5242420-cloud/HawalekNow/pull/10). S01–S06 Verified تقنيًا، وتنفيذS07 واختباراتهاالمركزة نجحا. كشفالتحققالمجمع مهلةقفلSeedمتزامنة (367passed/1failed/0skipped)، وأصلحتالمهلة ويجريتحققهاوإعادةالحلكاملًا قبلتحديثالحالةالنهائية وCI. لا دمج P03 أو بدء P04، و[التسليم](handoffs/S07-to-P04.md) Draft.

## السلوك والملكية

| الوحدة | العلاقات والقواعد |
| --- | --- |
| Catalog | Activity → Category بشجرة داخل النشاط نفسه، دون حد Business ثابت للعمق. فرادة الأسماء العربية والإنجليزية بين الأنشطة أو الأشقاء. FK مركبة تحمي الانتماء، وقفل SQL يحمي نقلين متزامنين من إنشاء دورة؛ RowVersion تحمي التحديث المفقود. |
| Locations | Governorate → City → Area؛ FK مركبة تمنع زوج محافظة/مدينة متعارضًا. نقل مدينة ذات مناطق يرفض409؛ نقل الفارغة أو نقل منطقة إلى زوج صحيح ذري. الاسم الإنجليزي ليس هوية ولا يفرد قسرًا. |
| Search | SynonymGroup → SynonymTerm؛ النص الأصلي محفوظ، ومفتاح FormKC/فراغات/ToUpperInvariant فريد عبر جميع المجموعات حتى المعطلة. لا دمج لأحرف العربية المختلفة أو حذف للتشكيل والشرطات. أمثلة تيشرت/تي شيرت/T-shirt/Tshirt مصطلحات صريحة. |

تعطيل الأب يخفي نسله من الاختيار العام دون تعديل IsActive المخزنة للأبناء، وإعادة تفعيله تحترم تعطيل الابن المستقل. نقل التصنيف داخل نشاطه يغير مسار ظهوره، ولا يغير نشاطه. الحذف الذي يترك أبناء يرفض409؛ لا ادعاء اختبار مراجع منتجات مستقبلية. حذف مجموعة مرادفات يحذف مصطلحاتها في المعاملة نفسها. كل CRUD متعدد التغييرات داخل UoW الحالية وDbContext واحدة، دون تجاوز ملكية وحدة أو مدخل عام لجداولها.

## خريطة HTTP الفعلية

جميع المسارات تحت /api/v1. الكتابة الإدارية تتطلب AdminOnly وHTTPS وCSRF، ومعدل60 mutation/دقيقة لكلpeer مشترك للبيانات المرجعية. الزائر401 والمستخدم العادي403؛ الإخفاقات بعقد ProblemDetails المغلق دون SQL أو أسرار. PUT/DELETE تحتاج RowVersion؛ DELETE تحملها في JSON body.

| المورد | العام | الإدارة |
| --- | --- | --- |
| الأنشطة | GET catalog/activities | GET/POST admin/catalog/activities؛ PUT/DELETE .../{id} |
| التصنيفات | GET catalog/categories?activityId&parentId | GET/POST admin/catalog/categories؛ PUT/DELETE .../{id} |
| المحافظات | GET locations/governorates | GET/POST admin/locations/governorates؛ PUT/DELETE .../{id} |
| المدن/الأقسام | GET locations/cities?governorateId | GET/POST admin/locations/cities؛ PUT/DELETE .../{id} |
| المناطق | GET locations/areas?governorateId&cityId | GET/POST admin/locations/areas؛ PUT/DELETE .../{id} |
| المرادفات | لا مسار عام أو بحث تجاري | GET/POST admin/search/synonym-groups؛ PUT/DELETE .../{id} |

الاختيار العام: language=ar/en، page من1 إلى100000 وpageSize من1 إلى100، وفرز ثابت بمعرف SQL وصفحة واحدة فقط. يعيد Id/Name دون RowVersion أو IsActive أو الاسمين معًا. الإدارة تعرض النسخة والحالة والأسماء، مع فلاتر الآباء للمواقع والتصنيفات. [دليل API](../../backend/API_EXPLORATION.md) و[Collection31طلبًا](../../backend/postman/HawalekNow.P03.postman_collection.json) و[Environment فارغة الأسرار](../../backend/postman/HawalekNow.P03.Local.postman_environment.json). JSON/scripts وحارس CSRF فُحصت؛ لم تنفذ رحلة Postman كاملة أو مراجعة المستخدم الشخصية.

## Seed وgeography وCache

المصدر المقبول [Open Admin Data](https://github.com/open-admin-data/egypt-administrative-divisions) عند76b47b22872288c0d8b0c757975980e6986945e8، بترخيص CC BY4.0 ونسب وبصمات محفوظة. النسخة تضم27 محافظة و365 قسمًا/مركزًا و5716 شياخة؛14 قسمًا بلا مناطق و18 مجموعة أسماء إنجليزية متكررة صحيحة. لا تكملة مخترعة أو ادعاء اكتمال مصر أو دقة إحداثياتها. إحداثيات المصدر لا تستورد. الأنشطة الأربعة من المتطلبات، والإنجليزية ترجمة عرض: Clothing and accessories؛ Electronics and mobile phones؛ Furniture and household goods؛ Car spare parts.

الأمر الصريح --seed-reference-data يضيف المفقود بمعاملة واحدة وCatalog ثمLocations، ويتحقق من المصدر قبل الكتابة. سجلات SourceKey دائمة تحفظ المعرفات وإعادة التشغيل حتى بعد حذف Admin؛ لا استبدال لأسمائه أو نقله أو تعطيله أو إعادة إحياء محذوفه. الفشل بعد حفظ الآباء يرجع الجميع. لا حسابات تنشأ أو HTTP/Jobs تبدأ في الأمر. [التشغيل الآمن](../../backend/REFERENCE_DATA_OPERATIONS.md) و[النسب والبصمات](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/SOURCE.md).

GeoPoint مستقلة عن EF، مع Latitude[-90,90] وLongitude[-180,180] finite. التحويل Infrastructure إلىPoint4326 يستخدم X=Longitude/Y=Latitude. SQL أثبتت roundtrip ومحاورها ومسافة درجة على خط الاستواء نحو111319.49متر بسماحية300متر. المستهلك الإنتاجي المحدد في المراجع P04-S04؛ صحح التصميم افتراض centroid قبل الكود، ولم يضف كيانًا أو endpoint أو جدولProbe إنتاجيًا. [الحزم وتراخيصها](../../backend/P03_DEPENDENCIES.md): مزودNTS10.0.14 MIT، وNTS2.6.0 وSqlServerBytes2.1.0 BSD-3-Clause. لا تغيير SDK أوMediatR أو مخطط اعتماد الإنتاج.

[Cache العامة](../../backend/REFERENCE_CACHE.md) تستخدم IMemoryCache مستقلة لكلHost، وSnapshot immutable محددة الحقول. المفاتيح تشمل اللغة والمورد والآباء والصفحات وفلتر الفعال؛ TTL مطلق60ثانية وحد8192وحدة بتكلفة1+عددالعناصر. إبطال بعدCommit فقط، وgeneration تحمي من إعادة نشر loader قديمة. لا تخزين حسابات أو صلاحيات أو Entities، ولاRedis أو اتساق موزع؛ Seed CLI قبلHTTP أو انتظار TTL للنسخ القائمة.

## Migrations والتحقق

أربع Migrations إضافية فقط: P03CatalogFoundation، P03LocationsHierarchy، P03ReferenceSeedKeys، P03SynonymGroups. لا Migration geography صورية؛ نوع وجدول التحقق داخل مشروع الاختبار وقاعدته المملوكة فقط. جميع الاختبارات تستخدم SQL فعلية وقواعد HN_P02_Test المملوكة للتشغيل، مع إبقاء خدمات المشاريع الأخرى وقاعدة التطبيق وVolumes دون حذف أو إعادة إنشاء.

النتائج المجمعة Pending حتى اكتمال التشغيل؛ الأعداد السابقة ليست افتراضًا للنجاح الجديد. [verification.json](verification.json) يحفظ نتائج كل خطوة، والفشل وإصلاحه بدل حذفه، ونتائج CI حسب رأسها الفعلي. فحوص الخطوات تثبت SQL constraints/ترقية/rollback/التزامن وHTTP الأمن والتفعيل والتصفح، وUnicode والإحداثيات وCache. التحقق النهائي يشمل locked restore وRelease Build وTests كاملة وFormat والفاحصين وdiff وبوابات الرفض وCI الفعلية.

## حدود الاعتماد والتسليم

الخطوات و[التصميم](DESIGN.md) و[فهرس الملفات](FILES.md) و[تسليم P04](handoffs/S07-to-P04.md) للمراجعة. مراجعة المستخدم وV4 Pending، والشرح التعليمي والتجربة الشخصية مهمة منفصلة؛ لا تقرير أو CI يعوضهما. لا تنفيذ P04 أوموافقات تجار أو متاجر أو فروع أو منتجات أو بحث تجاري أو Frontend أو توسعة Audit/Logging. ملفات Word وجدول TARGET_SCHEDULE.md لم تتغير؛ الجدول خارج staging.
