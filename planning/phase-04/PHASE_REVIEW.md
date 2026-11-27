# مراجعة P04 التاجر والمتجر والفروع

Updated: 2026-11-27
Status: InProgress
ExecutionAuthorization: D021
UserApproval: Pending
ReviewStatus: Pending
V4: Pending
P05Status: NotStarted

بدأ التنفيذ من main63b189618a9c856151920da7669a4e2c9af86461 بعد نجاح بوابة P03. S01 Verified تقنيًا مع85 اختبارًا مناسبًا ناجحًا؛ تأتي المراجعة الإدارية وAudit في S02. [التصميم](DESIGN.md) و[الأدلة](verification.json) و[S01](steps/S01.md). لا اعتماد بشري أوفحص غير منفذ مفترض. تسلم المرحلة كاملة InReview في Draft PR واحدة، مع تسليم P05 Draft دون بدء P05 أودمج P04.

S02 Verified محليًا مع139 اختبارًا مناسبًا وFormat وفاحصي التوثيق وGit وdiff. [دليل المسارات والمعاملة وAudit](../../backend/MERCHANT_OPERATIONS.md) و[تسليم S03](handoffs/S02-to-S03.md). CI على الرأس الجديد تنتظر الرفع؛ [CI S01](https://github.com/mohamedfathy5242420-cloud/HawalekNow/actions/runs/45605384702) أثبتت406 اختبارات على0c7c822 فقط. S03–S07 لم تبدأ بعد.

فشلت CI S02 الأولى45610300309 على122277c في Backend بـ45 فشلًا، والتوثيق وDocker ناجحان. أُعيدت S02 إلى InProgress وتسليم S03 إلى Draft، مع حفظ نتائج139 المحلية لنطاقها فقط. التصحيح والاختبارات الكاملة قيد التنفيذ؛ لا بدء S03 قبل نجاحهما وCI الجديدة.

نجحت إعادة التحقق المحلية بعد التصحيح:433 اختبارًا كاملًا، صفر failed/skipped، بناء0/0 وFormat verification. CI على رأس التصحيح هي البوابة التالية؛ لا بدء S03 بعد.

نجحت CI التصحيح45614038330 على5173c605ab558bc79dcc4fce5fa36024f5f2d25e بجميع الوظائف الثلاث و433/0/0 وبناء0/0. أصبحت S02 Verified وتسليم S03 InternalReady وبدأت S03 وفق D021. تبقىS04–S07 غير منفذة وUserApproval/V4Pending وP05NotStarted. الفشل السابق محفوظ في الأدلة وسجل الخطوة.

S03 نُفذت ومحليًا492/0/0 وبناء0/0 وlockedrestore/format/roadmap/Git/diff ناجحة. بوابةDocker وrestart وCI علىرأسالحفظ Pending؛ S04–S07 لمتبدأ. انظر[S03](steps/S03.md) و[تسليمS04](handoffs/S03-to-S04.md). تبقىUserApproval/V4Pending وP05NotStarted.

نجحتبوابةS03النهائية على31e8e3b:CI45846221851 و493اختبارًا وDocker/restart/UIDdenialمحليًا، وأصبحتVerified/S04handoffInternalReady. يبدأS04 وفقD021؛S05–S07لمتبدأ وP05محظورة. UserApproval/V4Pending والتجربةوالشرحDeferred.

S04Verifiedمحليًا:142فحصًا ثمبناء0/0 وإعادة33فحصًا وFormatverificationناجحة؛تسليمS05InternalReady. المواعيدلمتنفذبعدولاP05. تستمرP04InProgressواعتمادالمستخدم/V4Pending،وCIشاملةعلىرأسالتسليمالنهائيلازمة.

نجحت CI45853666822 على رأس S04 الفعلي f27b6ff3a3c8ef1b79acf2e37de70f4b9a86d951 بكل الوظائف الثلاث و523 اختبارًا (47 Architecture +319 Integration +157 Unit)، صفر فشل أوتخطٍّ وبناء0/0. أثبتت Docker194 مدخلًا مسموحًا و82 عينة ممنوعة وموارد Seed الثلاث وبقاء التخزين بعد restart ورفض UID آخر. بدأت [S05](steps/S05.md) للمواعيد تحت D021؛ أول بناء لاختباراتها فشل بـ13 خطأ صياغة C# في مُنشئات params الجديدة، وصُحّح دون تغيير سلوك الإنتاج؛ إعادة البناء والتحقق قيد التنفيذ. تبقى S06–S07 غير منفذة وUserApproval/V4 Pending.

S05 InReview/Verified محليًا:135 فحصًا ناجحًا، ثم Format verification وبناء0/0 وإعادة23 اختبارًا بعد إصلاح نافذة الفتح القادم لتشمل أسبوع فجوة DST. SQL/HTTP وترحيل بيانات S04 واختبار rollback مثبتة. [تسليم S06](handoffs/S05-to-S06.md) InternalReady؛ تبقى CI على رأس التسليم النهائي مطلوبة، وUserApproval/V4 Pending وP05NotStarted.

نجحت CI45856851203 على f7264282abb6bbd5c8d02070c545cb61ceadf7c7 بكل الوظائف و551 اختبارًا (47+324+180)، صفرفشل/تخطٍّ وبناء0/0، بما فيها حسابات القاهرة علىLinux وDocker/التخزين. بدأت [S06](steps/S06.md) بعد التحقق المناسب؛ بناء الإنتاج الأول0/0، والاختبارات قيد العمل. S07 لم تبدأ وUserApproval/V4Pending.

S06 InReview/Verified:197 فحصًا ناجحًا (47Architecture+100Integration+50Unit)، صفرفشل/تخطٍّ وبناء0/0 وFormat verification. أثبتت SQL/HTTP القبول والرفض والتزامن وrollback الفعلي معAudit وبقاءالنشاطالقديموظهورالفروع حتىقبولالطلب. [تسليم S07](handoffs/S06-to-S07.md)InternalReady. لا P05 أوMerge P04، واعتمادالمستخدم وV4Pending والتجربةوالشرحDeferred.
