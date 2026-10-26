# OpenAPI وScalar وAPI v1 وHealth — P01-S07

## الفكرة خطوة بخطوة

1. **OpenAPI** ملف JSON بيوصف HTTP API: المسارات، الطرق، المدخلات، أكواد الرد وأنواع المحتوى. بيتولد من Controllers والـmetadata الفعلية؛ مش قائمة وظائف مستقبلية.
2. **Scalar** واجهة بتقرأ الملف وتعرضه وتسمح بتجربة الطلب. ظهور HTML وحده مش دليل نجاح؛ لازم تشوف اسم API والمسار والعقود بعد تحميل JSON.
3. **Postman** عميل مستقل. Collection بتحفظ الطلبات والـassertions، وEnvironment بتحفظ baseUrl وقيم محلية غير حساسة. نجاح تشغيلها الآلي مش اعتماد بشري لـV4.
4. **api/v1** بادئة ثابتة لكل Controller من ApiV1RouteConvention في Program. Controller يكتب Route نسبي زي health، فيبقى api/v1/health. المسارات المطلقة / و~/ في Controller أو Action مرفوضة عند بناء model عشان متتجاوزش البادئة. لا مكتبة Versioning ولا تفاوض بالـheaders/query ولا أكثر من إصدار؛ v1 اسم مستند OpenAPI مستقل عن ده.
5. **Liveness** معناها التطبيق قادر يعالج طلب ويرد. HealthCheckService بيشغل بس checks عليها tag=live. مفيش checks تبعيات مسجلة في الإنتاج حاليًا؛ الوصول للـaction والرد هو فحص الحياة الأساسي الموثق من Microsoft. **Readiness** محتاجة شرط حقيقي للجاهزية، فمش موجودة هنا؛ لا ادعاء جاهزية SQL أو البريد أو الاستخدام التجاري.

## العقود المنفذة

| الطلب | البيئات | HTTP وContent-Type والـbody |
| --- | --- | --- |
| GET /api/v1/health/live | Development وProduction | 200 text/plain؛ Healthy. عند Unhealthy يرجع 503 text/plain؛ Unhealthy. Degraded في خدمة الصحة يعامل كـ200 وفق السلوك الأساسي |
| GET /openapi/v1.json | Development فقط | 200 application/json؛ OpenAPI 3.1 مع مسار فحص الحياة فقط |
| GET /scalar ثم /scalar/ | Development فقط | واجهة HTML وأصول JavaScript محلية من حزمة Scalar؛ تحميل JSON فعليًا مطلوب |
| GET /health/live أو /api/health/live أو /api/v2/health/live أو /api/v1/missing | كلاهما | 404 application/problem+json؛ code=resource.not_found |
| GET /api/v1/health/ready | كلاهما | 404؛ لا endpoint Readiness حاليًا |
| POST /api/v1/health/live | كلاهما | 405 application/problem+json؛ Allow يتضمن GET |
| /scalar وأصولها و/openapi/v1.json | Production | 404 application/problem+json؛ مفيش حماية auth مفترضة أو خيار يفتح توثيقًا عامًا |

Health بيرجع الحالة فقط، وCache-Control: no-store. لا أسماء checks أو Connection Strings أو SQL/server/runtime versions أو بيانات استثناء أو timings. 503 ليه body صريح، فـStatusCodePages مش بيحوله لـProblemDetails أو 500. فشل خدمة الصحة باستثناء غير متوقع يمر بمعالج S05 ويرجع 500 application/problem+json آمن. الـ503 والـ500 مختبرين بحقن أعطال داخل مشروع الاختبارات، بلا مسار أعطال عام.

كل رد فيه X-Correlation-ID. قيمة واحدة بطول 1–64 من ASCII A-Z/a-z/0-9/-/_ بترجع كما هي؛ غير كده Guid N جديد. المعرف metadata غير موثوق، مش هوية أو صلاحية. ProblemDetails فيه type/title/status/code/correlationId فقط، بنفس المعرف في header والـbody؛ لا detail/instance/path/exception. [سياسة S05](API_ERRORS.md) وتصحيح Success(null) و204 زي ما هم.

مستند OpenAPI بيوصف GET الحقيقي بأكواد 200 و503 كنص و500 كـProblemDetails الآمنة، ومدخل/مخرج Correlation. 404 لمسار مجهول و405 لطريقة تانية موضحين فوق وفي وصف المستند؛ مش عمليات وهمية. مفيش Hello/Product/CRUD أو probes اختبارات في مستند الإنتاج.

## التشغيل المحلي

من جذر المستودع؛ التطبيق لا يحتاج تشغيل SQL/Mailpit للخطوة دي ولا يغيرهما:

```powershell
dotnet restore backend/HawalekNow.slnx
dotnet restore backend/HawalekNow.slnx --locked-mode
dotnet build backend/HawalekNow.slnx --no-restore --disable-build-servers -m:1
dotnet run --project backend/src/HawalekNow.Api --no-build --no-restore --no-launch-profile -- --environment Development --urls http://127.0.0.1:5080
```

افتح [Scalar](http://127.0.0.1:5080/scalar/) و[OpenAPI JSON](http://127.0.0.1:5080/openapi/v1.json) و[فحص الحياة](http://127.0.0.1:5080/api/v1/health/live). إعداد Development صريح؛ no-launch-profile وحده مش بيختار البيئة. Scalar تستخدم JS محليًا وDisableDefaultFonts يمنع خطوط CDN الافتراضية؛ لا Proxy خارجي للطلبات.

لتجربة Production شغل instance منفصلة على منفذ آخر بنفس build:

```powershell
dotnet run --project backend/src/HawalekNow.Api --no-build --no-restore --no-launch-profile -- --environment Production --urls http://127.0.0.1:5081
```

دي تجربة محلية للبوابة وليست نشر Production. لا تغيير SDK أو Docker أو البيانات. أوقف العملية اللي شغلتها بـCtrl+C بعد التجربة؛ لا توقف الخدمات القائمة.

## تجربتك اليدوية في Scalar — Pending

1. افتح Scalar في Development. اتأكد من عنوان HawalekNow API وعملية GET /api/v1/health/live والعقود 200/503/500 وأنواع المحتوى، وعدم وجود probes أو وظائف تجارية.
2. افتح Test Request للعملية، ضع X-Correlation-ID بقيمة scalar-s07-review وابعت الطلب للعنوان المحلي. المتوقع 200 وHealthy وtext/plain ونفس المعرف في response header.
3. افتح JSON وشوف paths فيها فحص الحياة فقط. في Production افتح /scalar و/openapi/v1.json؛ المتوقع 404 آمن مع Correlation، وفحص الحياة يفضل 200.
4. راجع معنى Liveness وحدوده وعدم وجود Readiness. ابعت نتيجة تجربتك وأي ملاحظات؛ تفضل UserScalarReview Pending حتى تحصل فعلًا.

## تجربتك اليدوية في Postman — Pending

الملفان: [Collection](postman/HawalekNow.P01-S07.postman_collection.json) و[Environment](postman/HawalekNow.Local.postman_environment.json). مفيش كلمات مرور أو Tokens أو بيانات مستخدم؛ القيم baseUrl=http://127.0.0.1:5080 وcorrelationId=postman-s07-local وdocsStatus=200 فقط.

1. افتح Postman المثبت، واستورد الملفين كـCollection وEnvironment محليين. لا تثبيت تلقائي لأدوات أو Upload لبيانات أخرى. في الإصدارات اللي بتدعم ملفات المشروع افتح مجلد postman المحلي حسب الواجهة المتاحة.
2. اختار HawalekNow local Development وشغل Collection Runner مرة واحدة: 10 طلبات. اتأكد إن كل الـassertions ناجحة؛ تشمل حياة التطبيق، المعرف المرسل والمولد واستبدال غير الصالح، مسار مفقود وProblemDetails، البادئة الغلط وv2 وغياب Readiness و405، وOpenAPI وScalar.
3. لاختبار Production غيّر نسخة محلية من Environment إلى baseUrl=http://127.0.0.1:5081 وdocsStatus=404، وشغل نفس الطلبات. الـHealth يبقى 200 والتوثيق يبقى 404؛ لا تحفظ القيمة دي فوق Environment التطوير المتتبع.
4. اختبار Scalar داخل Postman بيتحقق من HTML والإعداد فقط؛ تحميل الواجهة فعليًا يتراجع في المتصفح. لا تحاول توليد 503 بإيقاف SQL/البريد؛ مش تبعيات للفحص ده. لا endpoint أعطال عام.
5. ابعت عدد الطلبات/الـassertions الناجحة وأي failure. UserPostmanReview وV4 Pending حتى تراجعها بنفسك؛ نجاح تنفيذ Agent لا يساوي اعتمادك.

## التحقق والحدود

ApiFoundationTests تستخدم WebApplicationFactory<Program> وتسجيل ومكونات الإنتاج الفعلية. تغيير البيئة وحقن check فاشل أو service بترمي استثناء أو إضافة Controller اختبار مخفي هما فقط تعديلات الاختبار؛ مفيش نسخة بديلة من Program. الـprobes لا تسجل في الإنتاج، وإضافة probe المخفي تختبر فعليًا وجوده في HTTP وغيابه من JSON. التصحيح للـLogger هو ownership مع preserveStaticLogger: true في Program؛ الفلتر والسياسة لا يتغيران ولا يستخدم التطبيق Log.Logger static. اختبارات S05 وعزل السجلات والتوازي والسرية وnull بلا تعديل، والاختبارات المعمارية بلا تخفيف.

نتائج التنفيذ الفعلية والفشل والإصلاح والمراجعة في [S07](../planning/phase-01/steps/S07.md)، والحزم والتراخيص والمصادر في [PACKAGES](PACKAGES.md). S07 تظل InReview عند التسليم وتسليم S08 Draft؛ لا دمج أو بدء S08 قبل اعتمادك.
