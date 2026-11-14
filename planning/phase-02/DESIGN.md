# تصميم P02 وقواعد الحساب والأمان

Updated: 2026-11-14
Status: InReview
ExecutionMode: Phase
UserApproval: Pending
V4: Pending

## بوابة البداية والمصادر

P01 Done وApproved وفق D013؛ Handoff Ready. تحقق GitHub في بداية التنفيذ: PR #8 closed وmerged=true، merged_at=2026-11-10T11:01:00Z. main المحلي والمحدث b7a5020، وشجرة العمل خالية من تغييرات متتبعة. أنشئ الفرع codex/p02-implement-identity-and-account-security من main بعد fetch وff-only. ملف docs/TARGET_SCHEDULE.md غير متتبع، وبصمته 26DC5102E591FA55D245C9294C3B2222B72DD6A57EA2E32AC29F999FDB5C2403؛ لا تعديل أو staging له.

المصدر: المتطلبات المعتمدة، الأقسام 2 و3 و8 و11 و14؛ خطة Backend، P02-S01 إلى S09 وDefinition of Done؛ D001–D014 وQ003-01/02/05/06. المصدر الأصلي يذكر بريدين مختلفين عند فصل العميل والتاجر؛ جواب المستخدم في هذه المهمة يعدل ذلك وفق D014، ولا يعاد تفسيره كقرار تقني.

## القواعد المنقولة والقرارات

| القاعدة | المرجع أو نوع الاختيار |
| --- | --- |
| ApplicationUser واحد، ونوع حساب ثابت، وحسابان مستقلان للعميل والتاجر | D005؛ لا تبديل أدوار أو حساب متعدد الأنواع |
| Email وUsername فريدان داخل AccountType، ويمكن تكرارهما بين النوعين | قرار المستخدم D014؛ Login وعمليات الاسترجاع تحدد النوع صراحة |
| المعرّفات تظل محجوزة بعد الحذف النهائي | قرار المستخدم D014؛ سجل حجز دائم ببصمة HMAC مع فصل النوع والغرض؛ مفتاح الحجز سر دائم يلزم نسخه احتياطيًا |
| Username وEmail وPassword وقبول الشروط عند التسجيل | طلب تنفيذ P02 ومرجع التسجيل؛ لا AccountType أو Roles في DTO التسجيل |
| تأكيد البريد مستقل عن موافقة التاجر؛ Pending يستطيع تجهيز حسابه | D010؛ MerchantAccount ملك Merchants |
| Soft Delete وRecovery OTP ومهلة 30 يومًا | المتطلبات قسم 3؛ لا حذف بيانات تجارية مستقبلية |
| منع الحذف الذاتي للـAdmin؛ الحذف والاسترجاع للعميل والتاجر | قرار المستخدم D016 في هذه المهمة |
| OTP Hash، صلاحية، محاولات، إعادة إرسال، استعمال واحد | الخطة S04؛ القيم العددية غير محددة في المرجعين |
| Cookies HttpOnly Secure، Refresh rotation، CSRF وRate Limits | D005 والخطة S05/S06؛ القيم العددية غير محددة |

اختيارات تقنية مقترحة للتنفيذ، وليست أرقامًا منقولة من المتطلبات: كلمة المرور 15–128 حرفًا، دون شروط تركيب إجبارية، مع حجب كلمات شائعة؛ OTP من ستة أرقام مولدة بـRandomNumberGenerator، صالح عشر دقائق، خمس محاولات للغرض مع استمرار العداد عبر إعادة الإرسال، وفاصل إرسال 60 ثانية؛ قفل التحقق 30 دقيقة عند استنفاد المحاولات؛ Access خمس دقائق وRefresh سبعة أيام مع حد مطلق للعائلة 30 يومًا. Login خمس محاولات فاشلة ثم قفل 15 دقيقة. لا ادعاء امتثال NIST أو MFA بالبريد. [NIST](https://pages.nist.gov/800-63-4/sp800-63b.html) مرجع لطول كلمة المرور وحدود OTP ومنع إعادة الاستخدام؛ القيم الأكثر تحديدًا اختيارات تنفيذية قابلة للمراجعة.

سياسات HTTP المقترحة لكل IP: تسجيل 5/10 دقائق، دخول 10/دقيقة، إرسال OTP واسترجاع 5/15 دقيقة، تحقق OTP 20/10 دقائق، وتجديد 30/دقيقة. عدادات المحاولة وإعادة الإرسال الخاصة بالحساب تحفظ في SQL؛ محدد HTTP في الذاكرة لا يثبت حماية موزعة. لا ثقة في X-Forwarded-For افتراضيًا؛ إعداد Proxy موثوق يحتاج طوبولوجيا معلومة. البحث التجاري خارج P02.

## الطبقات والكيانات والملكية

Domain.Identity يملك AccountType وقواعد الحالات المستقلة عن Framework. Infrastructure.Identity يملك ApplicationUser المشتق من IdentityUser<Guid> وEF mapping وstores وhashing وtokens. Domain.Merchants يملك MerchantAccount وحالة الاعتماد، وInfrastructure.Merchants ينفذ عقود Application.Merchants.Contracts. لا يعرض أي عقد Entity أو IdentityUser أو DbContext.

DbContext واحد تقني داخل Infrastructure/Shared/Persistence، يكتشف IEntityTypeConfiguration من assembly دون الاعتماد على أنواع الوحدات. Stores صريحة من UserOnlyStore بدل افتراض DbContext مشتق من IdentityDbContext؛ جداول Identity القياسية تضبط داخل الوحدة. Schemas: identity وmerchants، ثم operations للـOutbox وhangfire للتشغيل. لا Repository عام أو مدخل UoW مفتوح للجداول؛ عقود متخصصة لكل استخدام. المعرفات Guid v7 وRowVersion للتنافس. تبقى EF وASP.NET خارج Domain وApplication.

S01 يؤسس المستخدم وMerchantAccount والمعاملة وMigration فقط. تضاف كيانات OTP/Outbox والجلسات والحذف في خطواتها بالتتابع. لا انتقال قبل نجاح SQL Migration وrollback. لا EnsureDeleted أو إعادة إنشاء قاعدة موجودة. اختبار SQL ينشئ قاعدة جديدة باسم معزول مملوك للتشغيل؛ التطبيق يستخدم HawalekNow_Development صراحة.

## حدود المعاملات والأحداث والمهام المخططة

| العملية | الحد المخطط |
| --- | --- |
| تسجيل تاجر | إنشاء ApplicationUser وMerchantAccount عبر عقد Merchants داخل DbContext ومعاملة SQL واحدة؛ الفشل يرجع الكل |
| طلب OTP | تغيير challenge وحفظ نية إرسال encrypted Outbox في نفس المعاملة؛ لا Enqueue بعد SaveChanges كبديل |
| إرسال البريد | Hangfire job يحمل MessageId فقط؛ العامل يقرأ payload محميًا، مع lease وتكرار آمن في SQL؛ قبول المزود ثم فقد الرد قد ينتج بريدًا مكررًا |
| تأكيد/Reset/Recovery | قفل الحالة والتحقق من الغرض والصلاحية والعداد والاستخدام داخل معاملة واحدة؛ الاستعمال الفاشل يحفظ العداد |
| Refresh | استهلاك الرمز السابق وإنشاء التالي ذريًا؛ كشف إعادة الاستخدام يلغي العائلة؛ تنافس التجديد لا يصدر فرعين صالحين |
| Logout/Reset/Delete | إلغاء الجلسات المطلوبة في نفس المعاملة؛ التحقق من جلسة وحالة الحساب على كل طلب محمي يثبت أثر الإلغاء |
| الحذف النهائي | Job يحمل UserId ونسخة طلب الحذف؛ يعيد فحص الحالة والموعد داخل SQL، والاسترجاع يلغي الأهلية؛ يحتفظ بحجز المعرّفات فقط |

وظائف الجدولة المقترحة: Outbox dispatch وidentity deletion sweep مع Guard للحالة. لا Dashboard مكشوفة، ولا OTP/Password/Access/Refresh في arguments أو logs أو response. DataProtection keys مستقرة ومحفوظة خارج Git؛ فقدها يفشل فك الرسائل القديمة، ولا يسجل payload عند الخطأ. لا exactly-once ولا ضمان وصول لصندوق المستلم. Mailpit محلي فقط؛ لا Brevo أو بريد خارجي ضمن هذه المهمة.

## خريطة HTTP المخططة

كل المسارات تحت /api/v1. التفاصيل النهائية وأدلة HTTP تحدّث وقت تنفيذ الخطوات.

| المسار | الغرض والسياسة |
| --- | --- |
| GET identity/csrf | إنشاء cookie وrequest token لـCSRF؛ هذا token حماية الطلب وليس رمز جلسة |
| POST identity/customers/register، identity/merchants/register | تسجيل منفصل؛ Anonymous مع CSRF وحدود التسجيل |
| POST identity/email/request، identity/email/verify | تأكيد البريد مع AccountType وغرض ثابت؛ رسائل عامة |
| POST identity/login، identity/refresh، identity/logout | جلسة Cookies فقط وCSRF؛ Login يحدد AccountType |
| GET identity/me، identity/sessions | الحساب والجلسات الخاصة بالفاعل؛ لا UserId قادم من العميل |
| POST identity/password/request، identity/password/reset | استرجاع بالبريد وغرض منفصل، وإلغاء كل الجلسات |
| DELETE identity/account | حذف ناعم وإلغاء الجلسات؛ CSRF وإعادة إثبات كلمة المرور |
| POST identity/recovery/request، identity/recovery/verify | استرجاع خلال 30 يومًا بغرض مستقل |
| GET merchants/account | بيانات حساب التاجر الخاصة؛ VerifiedMerchantSetup يسمح Pending |
| GET merchants/account/publishing-access | حد ApprovedMerchant وإمكانية النشر فقط؛ لا متجر أو منتج أو موافقة جديدة |
| GET identity/admin/session | التحقق من جلسة AdminOnly دون تنفيذ إدارة مستقبلية |

## مصفوفة الصلاحيات المخططة

| الفاعل | me/sessions | VerifiedCustomer | MerchantSetup | ApprovedMerchant | AdminOnly |
| --- | --- | --- | --- | --- | --- |
| زائر | 401 | 401 | 401 | 401 | 401 |
| غير مؤكد بجلسة صالحة | 200 | 403 | 403 | 403 | 403 |
| عميل مؤكد | 200 | 200 | 403 | 403 | 403 |
| تاجر مؤكد Pending | 200 | 403 | 200 | 403 | 403 |
| تاجر مؤكد Approved في fixture فقط | 200 | 403 | 200 | 200 | 403 |
| حساب موقوف أو محذوف أو جلسة ملغاة | 401 | 401 | 401 | 401 | 401 |
| Admin مؤكد | 200 | 403 | 403 | 403 | 200 |

الصلاحيات تستمد من SQL والفاعل المصادق عليه؛ لا Roles/UserId من body. الحالات المعتمدة في اختبارات فقط؛ لا endpoint موافقة من P04. سيحدّث هذا التصميم إذا كشف التحقق حاجة فعلية، مع فصل أي قرار منتج جديد عن الاختيارات التقنية.

## قيود التحقق الحالية

السجل الأولي: Docker API فشلت لغياب Engine؛ شغّل Docker Desktop وفحصت الموارد دون إيقاف أو حذف خدمات أخرى. التحديث حتى S04: Migration فعلية مطبقة على قاعدة التطوير المسماة، و174 اختبارًا ناجحًا على SQL وProgram وMailpit، بما فيها Hangfire SQL worker/restart وlost ACK. [أدلة كل خطوة](verification.json) تفصل الفشل والإصلاح. CI ناجحة على S03 فقط حتى تشغيل الرأس الجديد؛ ليست دليلًا على الخطوات اللاحقة. لا إنتاج أو Brevo أو وصول مضمون لصندوق المستلم.

تنفيذ S04 يستخدم AES-GCM بمفتاح Outbox:EncryptionKey مستقل ثابت وIdentity:OtpKey لـHMAC، بدل DataProtection المقترحة أعلاه لpayload. الـjob تحمل CancellationToken فقط وتختار Outbox من SQL؛ لا secret أو email في args. Worker يستيقظ عند البدء وكل دقيقة، بlease دقيقتين وإرسال SMTP بمهلة 15 ثانية و10 محاولات وتأخير متزايد حتى خمس دقائق. تفاصيل القيم والفشل وحدود التشغيل في [دليل الهوية](../../backend/IDENTITY_OPERATIONS.md).

## نتيجة التصميم المنفذ

اكتملت S01–S09 تقنيًا؛ المرجع الحالي هو [مراجعة المرحلة](PHASE_REVIEW.md) و[الأدلة](verification.json). اختيار الجدولة النهائي هو job لكل طلب حذف بموعد وgeneration محفوظين، عبر encrypted Outbox وHangfire SQL، مع حراسة الحالة داخل المعاملة؛ لا sweep مفترضة أوDashboard عامة. أضيفت SubjectId للـOutbox وتنظيف الرسائل القديمة الآمن، وbinding لمفتاح حجز المعرّفات، وaudit تقنية مقتضبة وفق المتطلبات. تبقى القيم المقترحة الأولى أعلاه سجل التصميم؛ القيم المنفذة وتفاصيل الحدود موثقة في دليل الهوية.270 اختبارًا ناجحًا محليًا وفيCI على كود التنفيذ؛ UserApproval وV4 Pending وتسليمP03 Draft. لا Merge أوP03.
