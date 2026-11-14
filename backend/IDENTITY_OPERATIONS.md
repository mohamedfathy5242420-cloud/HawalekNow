# تشغيل هوية HawalekNow محليًا

Updated: 2026-11-10
Status: InProgress

## Admin bootstrap

يعمل الأمر المقصود مرة لإنشاء Admin الأول، ويعيد التشغيل دون تكرار أو تعديل أي حساب قائم. لا يطبق Migration أو يعيد إنشاء قاعدة. جهّز قاعدة HawalekNow_Development عبر Migration المراجعة أولًا، ثم وفّر الإعدادات التالية من Environment أو مخزن أسرار محلي. لا تحفظ قيمًا حقيقية في ملف متتبع أو سجل أو Collection.

| متغير Environment | الاستخدام |
| --- | --- |
| ConnectionStrings__Application | اتصال SQL بقاعدة التطبيق المحددة؛ ليس master أو قاعدة مشروع آخر |
| Identity__IdentifierKey | مفتاح HMAC عشوائي من 32 bytes بصيغة Base64، ثابت ومخزن بأمان خارج Git؛ يلزم للحجز الدائم ولا يعاد توليده بين مرات التشغيل |
| Identity__AdminSeed__Username | اسم Admin الأول؛ 3–64 من الحروف والأرقام ASCII و._- |
| Identity__AdminSeed__Email | بريد Admin يحدده المشغل |
| Identity__AdminSeed__Password | كلمة مرور سرية من 15–128 حرفًا؛ لا توجد قيمة افتراضية |

```powershell
dotnet run --project backend/src/HawalekNow.Api --configuration Release --no-build --no-launch-profile -- --seed-admin
```

ينتهي الأمر قبل تشغيل HTTP. نجاحه exit 0؛ الإعداد الناقص أو الخاطئ أو تعذر SQL exit 1 برسالة ثابتة دون قيم أو استثناء. ينشئ bootstrap الموثوق Admin مؤكد البريد. وجود Admin يترك PasswordHash والهوية والصلاحيات كما هي، حتى عند تغير الإعدادات؛ تعديل حساب قائم يحتاج إجراء مستقل مقصود. لا تعرض اسم المشغل أو البريد أو كلمة المرور في report أو logs.

## التحقق المعزول

```powershell
./backend/test-p02.ps1
```

محليًا يقرأ المشغل إعداد SQL الموجود في docker/.env دون عرضه. في CI يمرر HAWALEKNOW_TEST_SQL باتصال خاص بالتشغيل وDatabase=master؛ الاختبارات تنشئ قاعدة HN_P02_Test_<Guid> جديدة، وتطبق Migration حقيقية، وتنظف قواعدها بالاسم والملكية فقط. لا EF InMemory أو EnsureDeleted. تبقى قاعدة التطبيق وS06 probe وVolumes وموارد المشاريع الأخرى محفوظة.

S01: Migration وSQL والمعاملات وRowVersion؛ S02: bootstrap والتنافس والإعدادات وProgram الحقيقي. تعليمات الرحلات تضاف عند تنفيذ خطواتها. هذا دليل تطوير واختبار، وليس ادعاء جاهزية إنتاج أو تحقق Brevo.

## تأكيد البريد وOutbox

التسجيل يحفظ challenge وintent البريد داخل المعاملة نفسها. POST /api/v1/identity/email/request يستقبل accountType وemail ويعيد 204 عامًا؛ POST /api/v1/identity/email/verify يضيف code ويعيد 204 عند القبول، و400 آمنة عند الخطأ. الـOTP ستة أرقام، صالح عشر دقائق، استخدام واحد؛ خمس محاولات ثم قفل 30 دقيقة، واستمرار المحاولات عبر resend. فاصل 60 ثانية وثلاث رسائل/15 دقيقة لكل حساب وغرض. تأكيد بريد التاجر يبقيه Pending.

الإعدادات الإضافية للتشغيل HTTP/Worker، خارج Git:

| متغير Environment | الاستخدام |
| --- | --- |
| Identity__OtpKey | 32 bytes عشوائية Base64 لـHMAC، ثابتة بين Hosts حتى انتهاء الرموز القائمة |
| Outbox__EncryptionKey | 32 bytes مستقلة Base64 لـAES-GCM، ثابتة ومحفوظة مع نسخة احتياطية؛ فقدها يمنع قراءة الرسائل المعلقة |
| Email__SmtpHost | 127.0.0.1 أو localhost أو ::1 فقط في Development |
| Email__SmtpPort | منفذ SMTP لحاوية Mailpit المحلية |
| Background__Enabled | true افتراضيًا؛ false خاص بفحوص liveness والاختبارات التي لا تشغل Worker |

تُطبق Migration المراجعة قبل بدء العامل. Hangfire ينشئ schema hangfire في قاعدة التطبيق نفسها؛ عملية البدء تحتاج صلاحية إنشاء schema/jداول التشغيل. لا Dashboard منشورة. jobs تحمل CancellationToken فقط، ومعلومات البريد/OTP مشفرة في operations.Outbox؛ بعد الإرسال أو إسقاط رسالة قديمة يمسح payload. لا تغيّر مفاتيح قائمة دون ترحيل مقصود؛ لا تعالج رسالة غير قابلة للفك بإعادة توليد رمز عشوائيًا.

Outbox تدعي حتى 25 رسالة في كل تشغيل، بlease دقيقتين. الإرسال خارج معاملة SQL وبمهلة 15 ثانية؛ إعادة المحاولة بتأخير متزايد حتى خمس دقائق وحد عشر محاولات، ثم Failed محفوظة لتدخل تشغيلي مقصود. رمز الخطأ delivery.failed ثابت ولا يحفظ استثناء المزود. التشغيل عند البداية وكل دقيقة، وإعادة تشغيل العامل تستعيد leases المنتهية. معالجة SQL تمنع إعادة الرسالة Sent؛ إذا قبل SMTP الرسالة ثم فُقد ACK، قد تصل نسخة ثانية برمز التحقق نفسه. اختبار proxy إلى Mailpit يثبت ذلك؛ لا exactly-once أو ضمان الوصول لصندوق المستلم. SMTP بلا TLS محصور باتصال loopback للتطوير؛ Cookies الجلسة في S05 تظل Secure.

الناقل الحالي يرفض التشغيل خارج Development. لا إعداد Brevo أو إرسال خارجي أو ادعاء جاهزية بريد إنتاج؛ يلزم ناقل وإعداد وإذن مستقلان قبل ذلك. diagnostics العامة لـHangfire مغلقة لحماية البيانات؛ مراقبة وتشخيص تشغيل الإنتاج يحتاجان سياسة آمنة لاحقة.

اختبارات SMTP تحتاج HAWALEKNOW_TEST_SMTP_PORT وHAWALEKNOW_TEST_MAILPIT_URL. المشغل المحلي يقرأ metadata حاوية الاختبار من tmp/p02-mailpit-service.json إن وجدت، وCI ينشئ SQL وMailpit باسم وlabel خاصين بالتشغيل وبـports loopback عشوائية. لا حذف رسائل Mailpit المشتركة؛ كل اختبار يقرأ مستلمه العشوائي فقط. التنظيف يقتصر على قواعد الاختبار وحاويات التشغيل المملوكة.

## التسجيل

POST /api/v1/identity/customers/register وPOST /api/v1/identity/merchants/register يستقبلان username وemail وpassword وacceptTerms=true. لا Admin أو AccountType أو UserId قابل للاختيار. استجابة الطلب الصحيح 204 بلا body أو Cookies سواء أُنشئ الحساب أو كانت المعرّفات مستخدمة/محجوزة؛ لا تتخذها دليلًا على وجود حساب. الحساب الجديد غير مؤكد، والتاجر Pending. تسجيل النوعين بالمعرّفات نفسها ينشئ حسابين مستقلين وفق D014.

تقبل كلمة المرور 15–128 Unicode rune، مع قائمة صغيرة للكلمات الشائعة ودون شروط تركيب؛ لا ترسلها في query أو URL أو تقارير. وقت قبول الشروط يسجل UTC؛ لا يقدم P02 نصوص الشروط أو ادعاء نسخة قانونية منشورة. معاملة واحدة تضم UserManager وMerchantAccount؛ حفظ Identity الداخلي لا يهرب من rollback. المعرّفات النشطة والمحذوفة ناعمًا محجوزة بفهارس SQL؛ بعد purge تحفظ بصمات HMAC في جدول الحجز. فقد مفتاح IdentifierKey أو استبداله يفقد إمكانية مطابقة الحجوزات القديمة؛ يلزم حفظه ونسخه احتياطيًا، وأي rotation مستقبلية تحتاج ترحيلًا صريحًا يحفظ الحجوزات.

## الدخول وCookies

الإعدادات المطلوبة: Jwt__SigningKey مفتاح مستقل ثابت 32 bytes Base64، وJwt__Issuer وJwt__Audience قيمتان واضحتان للنظام نفسه، وليستا بريدًا أو أسرار مستخدم. يغلق التشغيل عند الغياب أو المفتاح غير الصحيح. لا يُنشئ fallback عند الإقلاع؛ احتفظ بالمفتاح خارج Git وبنسخة احتياطية. استبداله يبطل access القائمة؛ إدارة key rotation متعددة المفاتيح ليست جزءًا من هذا التنفيذ.

POST /api/v1/identity/login يستقبل accountType وidentifier وpassword. identifier بريد للعميل وAdmin، ويقبل للتاجر البريد أو Username وفق المتطلبات قسم3. الدخول صحيح البنية يعيد 204 بلا Body عند النجاح، و401 آمنة موحدة للخطأ/عدم الوجود/القفل. خمسة إخفاقات تقفل الحساب 15 دقيقة داخل SQL؛ لا تكشف الاستجابة سبب الرفض. المستخدم غير المؤكد يحصل على جلسة، والسياسات في S07 تحصر الوصول. POST /api/v1/identity/refresh بلا Body، ويقرأ refresh cookie فقط، ويعيد زوج Cookies جديدًا دون رمز في Body. GET /api/v1/identity/me يعيد هوية الجلسة الحالية فقط.

الـcookies هي __Host-HawalekNow.Access و__Host-HawalekNow.Refresh، HttpOnly وSecure وSameSite=Strict وPath=/ دون Domain. لا تقبل API بديل Authorization Bearer، ويرفض login/refresh غير HTTPS. استخدم HTTPS محليًا بشهادة تطوير موثوقة في عميلك؛ لا تعطل Secure لتجربة Postman/Scalar. اختبار الحل يشغل Kestrel/TLS فعليًا وعميلًا يثق بجذر اختبار خاص فقط، ولا يغير Trust store.

Access HS256 صالحة حتى خمس دقائق، مع signature/issuer/audience/exp/nbf ورفض algorithm آخر وClockSkew صفر. claims تحمل UserId/SessionId/Jti فقط؛ النوع وتأكيد البريد يقرآن من SQL، ولا تثق API بأدوار في token. كل طلب محمي يقرأ الجلسة من SQL: الإلغاء أو انتهاء الجلسة يؤثر على الطلب التالي بعد commit، ولا يوقف طلبًا بدأ قبل الإلغاء. لا cache لقرار صلاحية الجلسة. هذا يزيد اعتماد المصادقة على توافر SQL؛ liveness لا تعني readiness.

Refresh عشوائية 256-bit مع selector، وSHA256 فقط مخزنة في identity.RefreshCredentials. مدة خاملة سبعة أيام، وحد عائلة مطلق 30 يومًا لا يمتد بالدوران. كل تدوير يستهلك القديمة ويصدر التالية في معاملة وتحت قفل المستخدم. تقديم credential صحيحة مستهلكة يلغي العائلة؛ طلبان متنافسان قد يعطيان نجاحًا واحدًا ثم إلغاء العائلة بسبب reuse، فيحتاج العميل login جديدًا بدل retry أعمى لنفس refresh. لا يثبت فقدان cookie أو مسحها إلغاء SQL؛ logout وإدارة الجلسات تكتملان في S08. CSRF وHTTP rate limits تنفذان في S06 قبل التسليم؛ لا تعرض هذه الحالة الوسيطة كإصدار جاهز.

## CSRF وحدود HTTP

قبل أي POST/PUT/PATCH/DELETE، اطلب GET /api/v1/identity/csrf عبر HTTPS بالـcookies الحالية، ثم أرسل requestToken من الاستجابة في headerName المعاد (X-CSRF-TOKEN)، مع cookie نفسها. هذه request token لحماية CSRF وليست access أو refresh token. cookie __Host-HawalekNow.Csrf هي HttpOnly Secure SameSite=Strict وPath=/ بلا Domain. لا تحفظ requestToken أو Cookies في Collection/Environment متتبعة أو reports؛ لا قيمة ثابتة لها.

الحماية عامة لكل controller mutation، حتى anonymous registration/login/email OTP وreset/recovery عند إضافتها. تستخدم IAntiforgery.ValidateRequestAsync في authorization filter API، وتعيد ProblemDetails400 الحالية عند الغياب أو الخطأ؛ لا bypass attribute لمسار تعديل. طلب التعديل غير HTTPS يرفض403 قبل استدعاء verifier ذات cookie Secure، دون redirect أو تعطيل Secure. GET/HEAD/OPTIONS/TRACE لا تغير الحالة ولا تتطلب CSRF. أعد طلب token عند تغير المستخدم، وبعد انتهاء access قبل refresh ثم بعد نجاح refresh؛ token مرتبطة بالـprincipal الحالي. GET csrf نفسها ترفض HTTP حتى مع X-Forwarded-Proto مزيفة.

Security__KeyRingPath يحدد مجلد DataProtection مطلقًا ثابتًا خارج Git. هو مطلوب عند التشغيل خارج Development، ويتحقق منه startup من الإعداد النهائي عبر DI. Development تستخدم مستودع المنصة الافتراضي إن لم يحدد. ApplicationName ثابت HawalekNow؛ مفاتيح key ring تحتاج نسخًا احتياطيًا ووصولًا محصورًا بهوية التطبيق. عند المسار الصريح على Windows تشفر بـDPAPI للمستخدم الحالي؛ لا تفترض نقلها بين أجهزة/هويات مختلفة. على Linux لا يضيف هذا التنفيذ تشفير ملفات المفاتيح تلقائيًا؛ إعداد إنتاج يحتاج storage محمية وصلاحيات وتشفير مفاتيح ملائمًا قبل النشر. لا مفاتيح حقيقية داخل الصورة أو المصدر. اختبارات المضيف تستعمل provider مؤقتًا، واختبار restart فقط ينشئ مجلد مفاتيح عشوائيًا جديدًا، يثبت إعادة القراءة ثم ينظف مجلده وحده؛ لا حذف لمفاتيح المنصة القائمة.

الحدود لكل عنوان اتصال في كل instance: تسجيل العميل والتاجر معًا5/10 دقائق، login10/دقيقة، طلب رمز5/15دقيقة، تحقق رمز20/10دقائق، refresh30/دقيقة. queue=0؛ الرفض429 مع ProblemDetails آمنة وRetry-After وcorrelation. عدادات SQL للمحاولات وresend والقفل تبقى مستقلة ودائمة، ولا تصفر بتغير IP أو إعادة تشغيل HTTP host. HTTP counters في الذاكرة ولا تدعي حدودًا موزعة أو حماية DDoS كاملة.

IP من Connection.RemoteIpAddress فقط، مع توحيد IPv4-mapped IPv6؛ لا تفسر X-Forwarded-For أو X-Real-IP أو Forwarded من العميل. لا ForwardedHeaders middleware مفعلة هنا؛ نشر خلف proxy يحتاج إعدادًا مقصودًا لعناوين proxies والشبكات الموثوقة وHTTPS، ولم يُختبر لهذا النشر. اختبارات spoofing تغيّر headers ويبقى quota نفسها؛ peer آخر حقيقي في TestServer له partition مستقلة. اختبار التنافس الأصلي يسجل ستة طلبات من peer contexts مستقلة عبر Program مع CSRF صحيحة، لا يتجاوز limiter في الإنتاج. لا search endpoint أو سياسة بحث تجاري ضمن P02.

## صلاحيات الحساب وموارد المالك

| GET route تحت /api/v1 | السياسة والمخرج |
| --- | --- |
| identity/me | أي جلسة نشطة؛ UserId/SessionId/AccountType/EmailConfirmed الحالية |
| identity/sessions | جلسات المالك النشطة، حتى50 مع الحالية أولًا؛ تواريخ وRevokedAt وIsCurrent فقط |
| identity/sessions/{sessionId} | تفاصيل جلسة المالك؛ غير المملوكة أو المفقودة404 حتى للـAdmin |
| identity/customer-profile | VerifiedCustomer؛ UserId/Username/Email للعميل المؤكد نفسه |
| merchants/account | VerifiedMerchantSetup؛ حالة وإمكانات ملف التاجر الخاص به |
| merchants/account/publishing-access | ApprovedMerchant؛ استحقاق نشر لملف التاجر المعتمد نفسه |
| identity/admin/session | AdminOnly مؤكد البريد؛ معلومات جلسة Admin الحالية |

الزائر401، والنوع غير المطابق أو البريد غير المؤكد403. Pending وChangesRequested المؤكدتان تدخلان تجهيز الحساب؛ Approved المؤكدة تدخل التجهيز واستحقاق النشر؛ Rejected تدخل me/sessions فقط. Merchant Suspended أو User AccessStatus=Suspended/Deleted يرفض login/refresh وطلبات cookie سابقة401. دعم حالة Deleted هنا guard للمصادقة؛ S09 تضيف انتقال الحذف و30 يومًا والجدولة. لا يتحول Pending إلى Approved بتأكيد البريد.

AccessStatus column من Identity لحالة الوصول العامة، وMerchantApprovalStatus تظل ملك Merchants. Metadata authorization تقرأ النوع وتأكيد البريد من SQL، وMerchant handler يقرأ العقد العام scoped في كل طلب؛ تغيير SQL يؤثر على الطلب التالي دون إصدار JWT جديدة. الطلب الذي بدأ قبل تغيير الحالة قد يكمل؛ mutations الحرجة اللاحقة تحتاج guard داخل معاملتها أيضًا. لا Roles أو UserId من body/query تعطي صلاحيات، ولا تؤخذ claims إضافية من JWT بدل actor المبني من SQL. ملكية الجلسة جزء من WHERE مع UserId الموثوق، وquery identifier لحساب آخر لا يغير المالك.

الاختبارات تنشئ حالات Approved/ChangesRequested/Rejected/Suspended/Deleted صراحةً في قواعد SQL المملوكة لها، دون approval أو تعليق أو حذف API في S07. أسماء السياسات public contract داخل Application.Identity.Contracts، والتنفيذ Framework في Api.Identity؛ Merchants API تستخدم العقد فقط. لا Framework/EF في Application أو Domain، ولا Entity من وحدة أخرى.

## استرجاع كلمة المرور وإلغاء الجلسات

POST identity/password/request يستقبل accountType وemail ويعيد204 عامة؛ يُرسل PasswordReset OTP فقط للحساب المتاح. POST identity/password/reset يستقبل النوع والبريد وcode وnewPassword، ويعيد204 بلا Body عند القبول أو400 عامة. القواعد نفسها:10 دقائق وخمس محاولات وقفل30 دقيقة واستمرار العداد عبر resend. HMAC يفصل UserId/Purpose/Generation، وإصدار رمز جديد يستبعد آخر code محفوظة لكل غرض للحساب، بما فيها آخر resend، دون تخزينها مكشوفة. reset لا يؤكد البريد ولا يبدل نوع الحساب.

UserManager.RemovePasswordAsync ثم AddPasswordAsync يستعملان policy/hasher/store القائمة، داخل SQL transaction نفسها مع استهلاك OTP وإلغاء كل جلسات الهدف وتصفير login lockout. الحفظان الداخليان لا يهربان من rollback؛ فشل بعد removal الفعلية يعيد hash وstamp والرمز والجلسات. الخطوة لا تضيف token provider أو password crypto مخصصة. كل JWT وrefresh سابقة ترفض في الطلب التالي بسبب SQL revocation؛ يظل الطلب الذي بدأ قبل commit ضمن الحد الموثق سابقًا.

POST identity/logout يثبت ملكية العائلة من refresh cookie حتى عند انتهاء access، ويعيد204 عامة مع مسح cookies حتى عند غياب credential. POST identity/logout-all بجلسة نشطة يلغي جلسات المالك كله ويمسح الحالية. DELETE identity/sessions/{id} يلغي جلسة مملوكة فقط، وغير المملوكة404 حتى للـAdmin؛ إلغاء الحالية يمسح cookies. مسح cookie وحده لا يمثل إلغاء SQL. كل المسارات تستخدم HTTPS وCSRF وsession mutation quota30/دقيقة لكل peer؛ request/reset يرثان send-code/verify-code quotas. Admin يستعمل reset وlogout وإدارة جلساته، مع منع self-delete لاحقًا وفقD016.

Local reproducibility: test-p02.ps1 invokes start-p02-services.ps1 when test SMTP is not supplied. It verifies project ownership, starts only the existing project SQL and the dedicated labelled P02 Mailpit, waits for SQL Healthy, and refreshes ephemeral loopback ports after engine restart. Missing/replaced metadata fails closed; no volumes or other project services are stopped or deleted. CI supplies its own isolated SQL/SMTP environment and does not depend on local metadata.
## الحذف والاسترجاع خلال 30 يومًا

DELETE /api/v1/identity/account يتطلب جلسة نشطة وHTTPS وCSRF وكلمة المرور الحالية في password. يؤخذ UserId من actor فقط. Customer وMerchant يلغيان كل جلساتهما وOTP السابقة ويصيران Deleted، مع موعد UTC ثابت بعد 30 يومًا وgeneration جديدة وaudit مقتضبة ونية جدولة Outbox داخل معاملة واحدة. Admin يحصل على403 وفق D016. الطلب المكرر لا يمدد المهلة؛ جلسة الحساب المحذوف تصبح401 على الطلب التالي. إعادة إثبات كلمة المرور تستخدم عداد الدخول: خمس محاولات ثم قفل15 دقيقة. طلب بدأ قبل الحذف قد يكمل، والبريد الذي بدأ تسليمه خارجيًا لا يمكن استدعاؤه.

POST identity/recovery/request يقبل accountType وemail ويعيد204 عامة. الرسالة للعميل أو التاجر Deleted فقط قبل الموعد؛ POST identity/recovery/verify يضيف code بغرض AccountRecovery مستقل وبقواعد OTP نفسها. النجاح204 يعيد Active ويحرس المهمة القديمة بالحالة والنسخة. لا جلسة تلقائية أو إحياء لجلسة قديمة أو تغيير EmailConfirmed أو موافقة Merchant. عند now == PurgeAt تنتهي أهلية الاسترجاع حتى لو لم تعمل المهمة بعد. إعادة الدخول مطلوبة بعد الاسترجاع، ورسالة Recovery المتأخرة تُسقط إذا تجاوزت الموعد قبل معالجة Outbox.

معالج identity.deletion-schedule.v1 يفك payload مشفرة ويفحص الحالة والنسخة ثم يسجل Hangfire SQL job لموعد PurgeAt ويحفظ معرفها. المعاملتان منفصلتان؛ فقد ACK بعد قبول Hangfire قد يكرر المهمة. AccountPurgeJob تحمل UserId وgeneration وCancellationToken فقط. تقفل المعرّفات بترتيب ثابت ثم حالة المستخدم، وتفحص Deleted وnonAdmin والنسخة والموعد. الاسترجاع أو مهمة قديمة أو مستخدم مفقود ينتج NoOp. توقف Worker يؤخر التنفيذ؛ الموعد ليس ضمان تنفيذ في اللحظة نفسها، والتأخير لا يعيد حق Recovery. عشر محاولات ثم Failed تتطلب متابعة المشغل؛ Dashboard غير معروضة.

Purge داخل معاملة SQL واحدة تنظف ciphertext المخزنة للمالك، وتكتب HMAC reservations للبريد والاسم داخل AccountType، وتحذف sessions وrefresh وOTP وجداول Identity التابعة وApplicationUser وملف MerchantAccount الفارغ عبر عقد Merchants العام. لا تحذف Store أوProduct أوFavorite أوReport أو بيانات تجارية مستقبلية. المراحل المالكة توسع سياسة الإخفاء والاحتفاظ قبل إضافتها؛ الفشل يرجع المعاملة كلها. Audit الدائمة تحمل IDs تقنية وgeneration ونوع الانتقال وUTC فقط، دون بيانات دخول، وفق المتطلبات قسم3. رسائل Outbox السابقة لإضافة SubjectId تُربط بفك آمن داخل المعاملة قبل المسح؛ المفتاح الخاطئ يفشل الحذف دون حذف جزئي. بيانات Jobs وOutbox التقنية الباقية ليست بيانات دخول.

Identity:IdentifierKey سر مستقر مع نسخة احتياطية خارج Git. IdentifierKeyBinding تحفظ fingerprint ولا تحفظ المفتاح، وتمنع التسجيل وpurge عند تبديله. لا تستبدل المفتاح أو تحذف binding لتجاوز الأخطاء؛ مفتاح مفقود أو حجوزات قديمة بلا binding تحتاج استعادة موثوقة وتسوية تشغيلية قبل المتابعة. لا Migration تكتب سرًا أو تفك payload. Migration20261111082824 تضيف AccountDeletions وAccountDeletionAudit وIdentifierKeyBinding وOutbox.SubjectId nullable مع index؛ Up لا يحذف صفوفًا قائمة. Down حذف الإضافات وغير مطبق.

تعافي Docker المحلي بتاريخ2026-11-14: أخفقت البداية بخطأ1920 في sailor-ingest.sock. بعد ثبوت إغلاق المحرك والعمليات، حُفظ مجلد run كاملًا بإعادة تسمية محددة داخل Docker وأنشئ مجلد run جديد. عاد Engine29.7.2؛ بيانات VHDX وVolumes محفوظة، وخدمات المشاريع الأخرى لم تُوقف. المجلد المحفوظ متروك. [بلاغ المصدر](https://github.com/docker/desktop-feedback/issues/554) يصف حلًا مماثلًا، وليس تشخيصًا عامًا لكل أخطاء Docker.
