# مراجعة P02 الهوية والأمان

Phase: P02
Status: InReview
Updated: 2026-11-14
ExecutionMode: Phase
UserApproval: Pending
ReviewStatus: Pending
V4: Pending
NextStep: P03-S01

الفرع codex/p02-implement-identity-and-account-security بدأ من main=b7a502051ac52f06b57c541e24f4bdde270a3392 بعد اعتماد P01 ودمج PR8 وفق D013/D015. توجد [Draft PR9 واحدة](https://github.com/mohamedfathy5242420-cloud/HawalekNow/pull/9). اعتماد المستخدم وV4 ما زالا Pending؛ لا دمج أو بدء P03. [التصميم](DESIGN.md) و[الأدلة](verification.json) يحفظان القواعد والنتائج والأخفاقات. جلسة شرح الكود والطلبات والأحداث والتجربة التعليمية مهمة منفصلة بعد تسليم التنفيذ.

## الخطوات وأدلتها

| الخطوة | التنفيذ والتحقق | مراجعة المستخدم |
| --- | --- | --- |
| [S01](steps/S01.md) | Verified: Identity, MerchantAccount, UoW, SQL migration;133 tests | Pending |
| [S02](steps/S02.md) | Verified: explicit idempotent Admin bootstrap;139 tests | Pending |
| [S03](steps/S03.md) | Verified: separate registration and atomicity;164 tests | Pending |
| [S04](steps/S04.md) | Verified: OTP, encrypted Outbox, Hangfire SQL, SMTP/restart/lost ACK;174 tests | Pending |
| [S05](steps/S05.md) | Verified: cookie sessions, rotation/replay and real HTTPS;193 tests | Pending |
| [S06](steps/S06.md) | Verified: CSRF, quotas, peer/proxy and key-ring;210 tests | Pending |
| [S07](steps/S07.md) | Verified:14 HTTP authorization cases, ownership and live SQL state;231 tests | Pending |
| [S08](steps/S08.md) | Verified: password reset and session controls;247 tests in CI42985057946 | Pending |
| [S09](steps/S09.md) | Verified:270 full solution tests passed locally and in CI43586473657 | Pending |

## خريطة API الفعلية

كل المسارات التالية تحت /api/v1. يعرض Development فقط /openapi/v1.json و/scalar/. كل mutation تحتاج HTTPS وCSRF وquota المناسبة، ومنها العمليات المجهولة مثل التسجيل والدخول والاسترجاع. عمليات النجاح بلا بيانات تعيد204؛ لا token جلسة فيBody. X-Correlation-ID وعقد ProblemDetails ذي الحقول الخمسة محفوظان.

| Method / path | المدخل أو المخرج والسياسة |
| --- | --- |
| GET health/live | Liveness فقط؛200 أو503 نصية، دون readiness للتبعيات |
| GET identity/csrf | requestToken وheaderName مع cookie آمنة؛ يجدد عند تغير principal |
| POST identity/customers/register | username/email/password/acceptTerms؛ Customer ثابت؛204 عامة |
| POST identity/merchants/register | المدخلات نفسها؛ Merchant ثابت وملف Pending؛204 عامة |
| POST identity/email/request | accountType/email؛204 عامة |
| POST identity/email/verify | accountType/email/code؛ EmailConfirmation فقط؛204 أو400 |
| POST identity/login | accountType/identifier/password؛ email للعميل وAdmin، email أوusername للتاجر |
| POST identity/refresh | refresh cookie فقط؛ rotation؛204 أو401؛ reuse يلغي العائلة |
| GET identity/me | هوية actor من SQL؛200 أو401 |
| GET identity/customer-profile | VerifiedCustomer؛ بيانات المالك فقط |
| GET identity/sessions | حتى50 جلسة نشطة مملوكة، الحالية أولًا؛ دون credentials |
| GET identity/sessions/{sessionId} | المملوكة فقط؛ غيرها404 حتى Admin |
| DELETE identity/sessions/{sessionId} | إلغاء المملوكة؛204 أو404؛ مسح cookies إذا كانت الحالية |
| POST identity/logout | إثبات refresh وCSRF؛204 عامة، حتى بعد انتهاء access |
| POST identity/logout-all | جلسة نشطة؛ إلغاء كل جلسات actor ومسح cookies |
| GET identity/admin/session | AdminOnly المؤكد؛200/401/403 |
| GET merchants/account | VerifiedMerchantSetup؛ Pending/ChangesRequested/Approved المؤكدة |
| GET merchants/account/publishing-access | ApprovedMerchant المؤكد؛ استحقاق فقط، دون متجر أومنتج |
| POST identity/password/request | accountType/email؛204 عامة |
| POST identity/password/reset | accountType/email/code/newPassword؛ PasswordReset فقط؛204 أو400 |
| DELETE identity/account | actor وكلمة المرور؛ Customer/Merchant فقط؛204، وAdmin403 |
| POST identity/recovery/request | accountType/email؛204 عامة؛ Deleted nonAdmin قبل30 يومًا |
| POST identity/recovery/verify | accountType/email/code؛ AccountRecovery فقط؛204 أو400، دون login |

راجع [دليل Scalar/Postman](../../backend/API_EXPLORATION.md) و[تشغيل الهوية](../../backend/IDENTITY_OPERATIONS.md). Collection P02 تضم28 طلبًا وEnvironment بقيم أسرار فارغة. فُحص JSON وJavaScript وتهيئة CSRF؛ لم تُنفذ Collection كاملة ولم تحدث مراجعة المستخدم، لذلك V4 Pending.

## مصفوفة الصلاحيات والملكية

| Actor with a valid session | me/sessions | Customer profile | Merchant setup | Publishing | Admin session | Self-delete |
| --- | --- | --- | --- | --- | --- | --- |
| Anonymous |401 |401 |401 |401 |401 |401 |
| Unconfirmed Customer |200 |403 |403 |403 |403 |204 after password proof |
| Confirmed Customer |200 |200 |403 |403 |403 |204 after password proof |
| Unconfirmed Merchant |200 |403 |403 |403 |403 |204 after password proof |
| Confirmed Pending/ChangesRequested Merchant |200 |403 |200 |403 |403 |204 after password proof |
| Confirmed Approved Merchant |200 |403 |200 |200 |403 |204 after password proof |
| Confirmed Rejected Merchant |200 |403 |403 |403 |403 |204 after password proof |
| Suspended/Deleted/revoked session |401 |401 |401 |401 |401 |401 |
| Unconfirmed Admin |200 |403 |403 |403 |403 |403 |
| Confirmed Admin |200 |403 |403 |403 |200 |403 |

حالات HTTP الأربع عشرة في S07 تشمل تعليق Merchant وتعليق المستخدم وتغير الحالة بنفس cookie وclaims مزيفة. قراءة جلسة أخرى أو إلغاؤها تعطي404 حتى Admin؛ الملكية داخل SQL WHERE. لا يؤخذ UserId أوRoles من العميل. Recovery المجهولة تحتاج OTP لهوية ونوع وغرض وصلاحية محددة. موافقة التاجر ملك Merchants؛ تأكيد البريد لا يمنح Approved.

## حدود المعاملات والأحداث والمهام

| العملية | الذرية والتعافي |
| --- | --- |
| Registration | UserManager وterms وMerchantAccount عبر العقد العام وOTP وOutbox intent في SQL واحدة؛ فشل ملف التاجر يرجع الكل |
| OTP issuance | قفل الحالة وchallenge وencrypted Outbox داخل المعاملة نفسها؛ لا Enqueue بعد Save كبديل |
| Delivery | OutboxPump يطالب بـlease ثم يرسل خارجيًا؛ completion مشروط بالـlease، وrestart يسترد المنتهية |
| Sessions | قفل المستخدم والعائلة ودليل refresh؛ rotation واستهلاك السابقة ذريان؛ reuse يلغي العائلة |
| Password reset | استهلاك OTP وRemove/AddPassword وإلغاء كل جلسات الهدف في SQL واحدة؛ فشل ما بعد removal يعيد الكلمة والرمز والجلسات |
| Soft delete | كلمة مرور وDeleted وdeadline/generation وaudit وإلغاء sessions/OTP ونية جدولة في SQL واحدة |
| Recovery | قفل الحالة والموعد وOTP مرة واحدة وActive وaudit في SQL واحدة؛ لا إحياء جلسة أو تغيير موافقة |
| Scheduling | Outbox handler يفحص الحالة والنسخة ويسجل job في Hangfire SQL؛ معاملتان قد تكرران الجدولة عند فقد ACK |
| Purge | identifier locks ثم user-state lock؛ إعادة فحص الحالة والنسخة والموعد، وتنظيف ciphertext وHMAC reservations وبيانات P02 في SQL واحدة |

Queue=identity وWorker واحد حاليًا. Outbox تعمل عند البداية وكل دقيقة، مع lease دقيقتين وSMTP timeout15 ثانية وعشر محاولات وbackoff حتى خمس دقائق. AccountPurgeJob تحمل IDs فقط وتقبل التكرار؛ استرجاع الحساب أوgeneration أحدث يجعل القديمة NoOp. توقف Worker يؤخر الحذف ولا يمدد Recovery. لا Dashboard عامة. قبول مزود SMTP ثم فقد الرد قد يكرر الرسالة؛ الاختبار الحقيقي أثبت ذلك، فلا exactly-once أو ضمان وصول صندوق المستلم.

## الملفات والحزم والمعمارية

Domain وApplication مستقلتان عن ASP.NET وEF. ApplicationUser وIdentity stores وEF وJWT وSMTP وHangfire في Infrastructure. يوجد DbContext محايد واحد يكتشف configurations، وschemas identity/merchants/operations/hangfire. Merchants يملك MerchantAccount وموافقته؛ عقده العام يتبادل IDs وDTOs فقط. توصيل API إلى Infrastructure في Program composition root، وShared لا يعتمد على تنفيذ وحدة. لا generic repository إضافي.

ملفات S09 الجديدة الأساسية هي AccountDeletion في Domain، وAccountLifecycle في Application، وAccountLifecycle/AccountPurgeJob/AccountDeletionConfiguration في Infrastructure، وAccountLifecycleController في Api، واختبارات AccountLifecycleTests وAccountDeletionTests. Migration20261111082824 تضيف ثلاثة جداول وOutbox.SubjectId اختيارية مع index، دون حذف صفوف قائمة. تشمل [Files changed](https://github.com/mohamedfathy5242420-cloud/HawalekNow/pull/9/files) ملفات المرحلة، و[PACKAGES](../../backend/PACKAGES.md) يسجل الإصدارات والتراخيص والمصادر. لا SDK/MediatR جديدان أوحزم جديدة في S08/S09.

## التحقق والقيود الحالية

[CI S01–S09](https://github.com/mohamedfathy5242420-cloud/HawalekNow/actions/runs/43586473657) على4c410147ad4e8ca7773ff382f90a285797939804 نجحت بكل Jobs.270 اختبارًا نجحت محليًا وفيCI:47 Architecture+191 Integration+32 Unit،0 failed/skipped، وBuild0 warnings/errors. locked Restore وFormat verification والفاحصان و60 حالة بوابة نجحت. Migration الإضافية مطبقة على قاعدة التطوير المسماة. فشل Docker وSQL السابق محفوظ في verification.json ولا يتحول إلىPassed. آخر commit لتوثيق التسليم يحصل علىCI مستقلة تُرصد فيPR والتقرير النهائي دون إعادة كتابة الدليل ليشير إلى نفسه.

التحقق المحلي يستخدم SQL Server فعلية وProgram وKestrel HTTPS وMailpit وHangfire SQL. لا اختبار إنتاج أوBrevo أوبريد خارجي. المعرّفان محجوزان دائمًا داخل AccountType وفق D014؛ مفتاح HMAC ثابت ومنسوخ بأمان وbinding ترفض تبديله. key-ring يحتاج تخزينًا وأذونات وحماية مناسبة للنشر؛ Windows يستخدم DPAPI، وLinux يحتاج حماية التخزين. حدود HTTP في الذاكرة وليست موزعة، ولا ثقة في ForwardedHeaders عشوائية. SQL live checks تمنع الوصول على الطلب التالي؛ طلب بدأ قبل تغير الحالة قد يكمل. لا حذف لبيانات تجارية مستقبلية أوReports؛ المراحل المالكة توسع سياسة الإخفاء والاحتفاظ قبل استخدامها.

LoggingPolicy: PipelineOnly; HostLogsBlocked; OperationalDiagnosticsIncomplete. لا bodies أوheaders أوtokens أوتفاصيل استثناءات في السجلات. TARGET_SCHEDULE خارج staging وبصمته الأصلية محفوظة. التنظيف محصور بقواعد وموارد اختبار محددة ومملوكة للتشغيل؛ Volumes وبيانات المشاريع الأخرى محفوظة.

## ما ينتظر المراجعة

V4 وتجربة المستخدم في Scalar/Postman واعتماد P02 تظل Pending. عند اكتمال الأدلة تسلم المرحلة InReview وتسليم P03 بحالة Draft. الدمج وبدء P03 يتطلبان إذنهما. الشرح التعليمي المفصل مهمة منفصلة بعد التسليم.

[تسليم P03](handoffs/S09-to-P03.md) Draft؛ [فهرس الملفات المتغيرة](FILES.md) يربط الملفات الجديدة والمعدلة من قاعدة main المعتمدة. تبقى مراجعة المستخدم وV4 Pending رغم نجاح التحقق التقني.
