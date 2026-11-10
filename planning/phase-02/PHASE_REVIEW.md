# مراجعة P02 الهوية والأمان

Phase: P02
Status: InProgress
Updated: 2026-11-10
ExecutionMode: Phase
UserApproval: Pending
ReviewStatus: Pending
V4: Pending
NextStep: P03-S01

المرحلة قيد التنفيذ. [التصميم](DESIGN.md) و[الأدلة](verification.json) و[S01](steps/S01.md). لا اعتماد أو دمج أو بدء P03؛ جلسة شرح الكود والطلبات والأحداث والتجربة التعليمية مهمة منفصلة بعد تسليم التنفيذ. لا تعد هذه الوثيقة تقرير إنجاز قبل اكتمال S01–S09 والتحقق الفعلي.

| الخطوة | التنفيذ والتحقق | مراجعة المستخدم |
| --- | --- | --- |
| S01 | Verified؛ Migration وSQL و133 اختبارًا ناجحًا | Pending |
| S02 | Verified؛ bootstrap وProgram الحقيقي والتنافس و139 اختبارًا ناجحًا | Pending |
| S03 | Verified؛ تسجيل النوعين ومعاملة SQL و164 اختبارًا ناجحًا | Pending |
| S04 | Verified؛ OTP وOutbox وHangfire SQL وMailpit/restart/lost ACK و174 اختبارًا ناجحًا | Pending |
| S05 | Verified؛ SQL sessions وJWT cookie/rotation/replay وKestrel HTTPS و193 اختبارًا ناجحًا | Pending |
| S06 | Verified؛ CSRF وHTTP quotas وproxy/key-ring/HTTPS checks؛ 210 اختبارًا ناجحًا | Pending |
| S07–S09 | NotStarted | Pending |
