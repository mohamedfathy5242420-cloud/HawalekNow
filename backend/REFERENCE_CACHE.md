# ذاكرة الاختيار العام في P03-S07

المستهلكان الفعليان: GET Catalog activities/categories و Locations governorates/cities/areas. Decorators داخل الوحدة المالكة تحوّل الصفحة إلى Snapshot immutable من Id/Name ومعلومات الصفحة فقط. القراءة الإدارية والمرادفات والحسابات والصلاحيات لا تدخل Cache. لا Entities متتبعة ولا object cache عامة ولا Redis.

المفتاح: Family + Resource + Language(ar/en) + OwnerId + ParentId + Page + PageSize + ActiveOnly=true. Catalog categories تستخدم ActivityId/ParentId؛ Locations تستخدم GovernorateId/CityId. الصفحةحدها 100، وأسماء العرضحدها 100 أو 160. لا تحفظالطلباتالفاشلة. يبقى SQL المصدرعندأي miss أوعدمقبولإدخال جديد بسببالحجم.

تنفيذ IMemoryCache مستقل ومملوك لكل Host، وفق[إرشادات Microsoft](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0). حد 8192 وحدة، تكلفةالصفحة 1+عددالعناصر؛ الوحدات ليستبايتات أوقياس RAM. AbsoluteTTL60 ثانية؛ لايمددها hit. TimeProvider يسمحباختبارالانتهاء دونانتظارحقيقي، معانتهاء IMemoryCache نفسه أيضًا. SizeLimit لايطبقعلى Cacheframework العامة ولايضبطموارد Identity.

Catalog و Locations لكلمنهما generation. التقاطالجيل و lookup و publish والإبطال تحميهابوابةقصيرة؛ SQL خارجها. بعد Commit فقط تبطلعملياتالإدارة family كاملة لأن تغييرأب يؤثرعلىأحفاده وصفحاتهم ولغاتهم. rollback ونسخةقديمة ورفضالعلاقات لايبطلان. الصفحاتمنجيلسابق تبقىمحجوبة منطقيًا وتختفيبالعمر، وتظلمحتسبةداخلالحد. loader بدأقبلالإبطال قديردباللقطةالقديمةعلىطلبه الجاري، لكنه لايمكنه نشرهافيالجيلالجديد حتىلوأكملبعدقراءةجديدة؛ لاادعاءإلغاءالطلباتالجارية.

Seed تتبعنفسالقاعدة: Catalog عبرعقده العام و Locations بعد Commit المشترك فقط، لا أثناء SaveChanges الجزئية. CLI لها Host مستقل؛ لذلكشغّل Seed قبل HTTP أوانتظر TTL لل Hosts القائمة. IMemoryCache محليةللنسخة؛ تحديث HostA أو SQL مباشرة لايبلغ HostB. لا ضماناتاتساقموزع أوتحديثفوريعبرالنسخ، ولااستنتاجصلاحياتمن Cache.

UnitTests تختبرالتطبيعالتقني للمفتاح و TTL والحد والفشل والجيل والـ immutable snapshot؛ أضيفمرجع Infrastructure لمشروع UnitTests لاختبارالتنفيذمباشرة دون SQL، ولميتغيرمخططاعتمادالإنتاج. هذايحملحزم Infrastructure السابقة إلى graph الاختبار فقط (منها Newtonsoft13.0.4 بدل 13.0.3)، دونترقيةحزمةإنتاج. Integration/HTTP تستخدم Program الفعلي و SQL: لقطةقديمةتتوقفبعد SQL، ثم AdminCommit و GET جديدة وإكمالالقديمة، وكذلكفشل UPDATE إجباري ونسخةقديمة وحفظالقيمةالمخزنة. اختبار Seed يحميالإبطال بعد Commit ويثبتعدمإبطال rollback.
