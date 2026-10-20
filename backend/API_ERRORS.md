# نتائج التطبيق وأخطاء HTTP — P01-S05

OwnerStep: P01-S05
Updated: 2026-10-19

## الفكرة خطوة بخطوة

1. تخيل عملية بتطلب مورد: لو العملية تمت، ترجع `Result<T>.Success(value)`؛ لو المورد مش موجود، ترجع `Result<T>.Failure(new Error(ErrorKind.NotFound))`. ده شرح تقني، مفيش Use Case تجاري اتنفذ. نجاح من غير بيانات يستخدم `Result.Success()`.
2. `Error` شايل نوع من قائمة مقفولة، ومفيش رسالة حرة أو قيمة مستخدم جواه. الـconstructors بتاعة Result private؛ عندك Success أو Failure بس. Failure بيرفض null، وError بيرفض enum خارج القائمة، فمينفعش نجاح يحمل خطأ أو فشل من غير خطأ. الأنواع classes sealed عشان default للـstruct ما يعملش حالة ثالثة.
3. `IsSuccess` متحسب من وجود Error؛ مفيش bool مستقل ممكن يتعارض معاه. `Value` بترجع القيمة وقت النجاح، ولو قرأتها وقت الفشل بترمي InvalidOperationException برسالة ثابتة. لو T نوع nullable، Success(null) مسموح؛ معنى النجاح إن العملية اكتملت. مفيش implicit conversion أو تغيير تلقائي للـHandlers.
4. الـController يستدعي `result.ToActionResult(HttpContext)` في Api. نجاح `Result<T>` بيرجع 200 حتى لو القيمة null؛ ساعتها Content-Type هو application/json والـbody نص JSON `null`. القيمة العادية ترجع 200 مع البيانات، و`Result.Success()` غير generic هو اللي يرجع 204 من غير body أو Content-Type. الفشل بيتحول حسب الجدول. Application ما تعرفش HTTP أو ASP.NET أو ProblemDetails؛ conversion في Api/Shared/Errors فقط.
5. الطلب عبر MediatR لسه بيعدي Logging ثم Performance ثم Validation ثم Handler. فشل validation يمنع Handler ويرمي ValidationException زي S04؛ Api يحوله 400. لو Handler رمى exception غير متوقعة، الرد 500 آمن. Result failure اللي رجع بشكل طبيعي معناه إن تنفيذ Send اكتمل، فعشان نحافظ على دلالة S04 يفضل Outcome=Succeeded في التسجيل، حتى لو النتيجة التجارية فشل متوقع؛ الاستثناء Outcome=Failed، والإلغاء Outcome=Canceled. مفيش إعادة تفسير عامة للـPipeline.
6. Correlation middleware بيهيئ المعرف مرة واحدة في scoped CorrelationContext ويرجعه في X-Correlation-ID. الـPipeline والـProblemDetails بياخدوا نفس القيمة، فالرد والسجل مرتبطين. بعده ExceptionHandler ثم StatusCodePages ثم Controllers؛ ترتيب endpoint routing اللي ASP.NET بيضيفه تلقائيًا ما يغيرش تغليف تنفيذ endpoint بالمعالجة دي.
7. IExceptionHandler يعالج validation والاستثناءات. MVC InvalidModelStateResponseFactory يعالج JSON وتحويل الأنواع والتحقق التلقائي. SafeProblemDetailsFactory يحمي أخطاء MVC الأخرى، وAddProblemDetails يثبت نفس السياسة عند استعمال الخدمة. StatusCodePages يملأ أخطاء فارغة زي 404 و405، من غير تغيير status أو Allow header. الرد اللي عنده body أو بدأ بالفعل ما يتكتبش عليه.
8. لو العميل لغى الطلب، ASP.NET يعالج OperationCanceledException/IOException مع RequestAborted كـ499 داخلي بلا ProblemDetails؛ العميل المنقطع قد لا يستقبل ردًا أصلًا. Handler فيه نفس الحراسة لو استدعي مباشرة. OperationCanceledException من غير RequestAborted إشارة عطل غير متوقع ويرجع 500؛ مش كل إلغاء معناه إن العميل قطع. بعد Response.HasStarted المعالجة ما تعيدش كتابة status/body؛ الاستثناء ينتقل والاتصال يفشل.

## HTTP mappings المطبقة

| المصدر | HTTP | code الآمن |
| --- | --- | --- |
| Result.Success بلا قيمة | 204 | لا خطأ |
| Result<T>.Success، بما فيها null | 200 مع application/json وJSON null للقيمة null | لا خطأ |
| ErrorKind.NotFound أو MVC/routing 404 | 404 | resource.not_found |
| ErrorKind.Conflict | 409 | resource.conflict |
| ErrorKind.Validation أو FluentValidation.ValidationException أو Model Binding/automatic validation | 400 | request.invalid |
| ErrorKind.Unauthorized | 401 | request.unauthorized |
| ErrorKind.Forbidden | 403 | request.forbidden |
| route موجود وطريقة HTTP غير مسموحة | 405 | request.method_not_allowed؛ Allow محفوظ |
| Content-Type غير مدعوم من MVC | 415 | request.unsupported_media_type |
| exception غير متوقعة | 500 | server.error |
| HTTP 4xx آخر فارغ من MVC/status pages | وضع HTTP الأصلي | request.failed؛ لا يتحول إلى 500 |
| إلغاء/انقطاع مرتبط بـRequestAborted | 499 داخلي | لا body؛ ليس 500 |

401 هنا mapping تقني فقط؛ لا Identity أو auth scheme أو challenge في S05. عند تأسيس المصادقة، challenge وWWW-Authenticate وصلاحيات endpoints مسؤولية نظام المصادقة، ولا تعتبر Result بديلًا له. StatusCodePages لا يعيد كتابة headers أو status لأي خطأ فارغ آخر؛ الأكواد غير الموجودة في الجدول تحتفظ بوضع HTTP الأصلي وتستخدم عنوانًا عامًا، ولا يحولها إلى 500.

## شكل الرد وسياسة السرية

أخطاء الجدول HTTP body نوعه application/problem+json، ويحتوي type=about:blank وtitle ثابت وstatus وcode وcorrelationId. لا instance/path ولا detail ولا stackTrace ولا request metadata. about:blank يستخدم معنى HTTP القياسي دون اختراع صفحة type غير موجودة.
الـValidation كلها لها code=request.invalid ورسالة `The request is invalid.`؛ لا نسخ ErrorMessage أو ErrorCode أو PropertyName أو AttemptedValue أو CustomState من FluentValidation أو ModelState. حتى ErrorCode اللي شكله آمن مش ثقة تلقائيًا؛ اختبارات بتدخل أسرار في كل المصادر دي وتتأكد إنها مش ظاهرة. قاموس رسائل حقول عامة يضاف لما يوجد استخدام حقيقي وتراجع رسائله، مش من أسماء أو قيم يحددها المستخدم. Password وOTP وTokens وbody/query/headers لا تدخل ProblemDetails أو السجل.
Correlation ID يظل metadata غير موثوق، قد يأتي من العميل حسب سياسة S04؛ لا يحمل هوية أو صلاحية. كل رد خطأ له نفس header وextension، والقيم غير الصالحة تستبدل Guid N.

## السجل والاختبارات

SafePipelineLogFilter لم يتغير: ثلاثة templates ومخطط مفاتيح مغلق، لا Exception objects أو Host logs. ASP.NET يضيف RequestId/RequestPath/ConnectionId وMVC يضيف ActionId/ActionName لـscopes، فـRemoveHostMetadata يتخلص منها قبل الفلتر؛ الخصائص الأخرى غير المسموحة لا تزال تمنع الحدث. SuppressDiagnosticsCallback يمنع telemetry الاستثناءات المعالجة، والفلتر يحجب host events الأخرى. لا حدث exception جديد؛ التشخيص التشغيلي غير مكتمل، وإضافته يحتاج قرار مخطط تسجيل مستقل.
[ResultTests](tests/UnitTests/ResultTests.cs) تختبر الحالات الصحيحة والخاطئة وValue؛ [ApiErrorTests](tests/IntegrationTests/ApiErrorTests.cs) ترسل HTTP حقيقي بـTestServer مع تسجيل ومكونات الإنتاج، وتغطي الجدول والسرية والـCorrelation وbinding ورؤوس HTTP والإلغاء الفعلي وresponse started. [PipelineTests](tests/IntegrationTests/PipelineTests.cs) تحفظ اختبارات S04 وترتيب النجاح والفشل والإلغاء. كل probes وControllers/Handlers داخل مشروع الاختبارات فقط؛ Program لا يسجل assembly الاختبارات. الاختبارات المعمارية باقية دون تعديل أو تخفيف.

تصحيح مراجعة 2026-10-19: التنفيذ السابق استخدم OkObjectResult(null)، وMVC حوّل الرد إلى 204 بدل عقد 200 الموثق. اختبار HTTP الجديد فشل أولًا بـExpected OK / Actual NoContent، قبل تعديل كود الإنتاج. الإصلاح محلي في ToActionResult<T>: يستخدم JsonResult(null) مع status=200 وapplication/json للقيمة null فقط، ويترك OkObjectResult للقيمة العادية. لا تعديل للـformatters أو إعداد MVC العام، ولا منع Success(null). الاختبار يتحقق أيضًا أن Ok(null) العادي خارج تحويل Result يظل 204، وأن Status/Content-Type/body/Correlation ID مطابقة للعقد في حالات null والقيمة الفعلية وResult بدون بيانات. النتائج النهائية في سجل S05، والسجل التاريخي السابق محفوظ.
