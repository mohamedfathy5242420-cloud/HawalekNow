# ذاكرة الاختيار العام في P03-S07

المستهلكان الفعليان: GET Catalog activities/categories وLocations governorates/cities/areas. Decorators داخل الوحدة المالكة تحوّل الصفحة إلى Snapshot immutable منId/Name ومعلومات الصفحة فقط. القراءة الإدارية والمرادفات والحسابات والصلاحيات لا تدخلCache. لا Entities متتبعة ولاobject cache عامة ولاRedis.

المفتاح: Family + Resource + Language(ar/en) + OwnerId + ParentId + Page + PageSize + ActiveOnly=true. Catalog categories تستخدمActivityId/ParentId؛ Locations تستخدمGovernorateId/CityId. الصفحةحدها100، وأسماء العرضحدها100 أو160. لا تحفظالطلباتالفاشلة. يبقىSQL المصدرعندأيmiss أوعدمقبولإدخال جديد بسببالحجم.

تنفيذ IMemoryCache مستقل ومملوك لكلHost، وفق[إرشاداتMicrosoft](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0). حد8192وحدة، تكلفةالصفحة1+عددالعناصر؛ الوحدات ليستبايتات أوقياسRAM. AbsoluteTTL60ثانية؛ لايمددهاhit. TimeProvider يسمحباختبارالانتهاء دونانتظارحقيقي، معانتهاءIMemoryCache نفسه أيضًا. SizeLimit لايطبقعلىCacheframework العامة ولايضبطمواردIdentity.

Catalog وLocations لكلمنهماgeneration. التقاطالجيل وlookup وpublish والإبطال تحميهابوابةقصيرة؛ SQL خارجها. بعدCommit فقط تبطلعملياتالإدارة family كاملة لأن تغييرأب يؤثرعلىأحفاده وصفحاتهم ولغاتهم. rollback ونسخةقديمة ورفضالعلاقات لايبطلان. الصفحاتمنجيلسابق تبقىمحجوبة منطقيًا وتختفيبالعمر، وتظلمحتسبةداخلالحد. loader بدأقبلالإبطال قديردباللقطةالقديمةعلىطلبه الجاري، لكنه لايمكنه نشرهافيالجيلالجديد حتىلوأكملبعدقراءةجديدة؛ لاادعاءإلغاءالطلباتالجارية.

Seed تتبعنفسالقاعدة: Catalog عبرعقده العام وLocations بعدCommit المشترك فقط، لا أثناءSaveChanges الجزئية. CLI لهاHost مستقل؛ لذلكشغّلSeed قبلHTTP أوانتظرTTL للHosts القائمة. IMemoryCache محليةللنسخة؛ تحديثHostA أوSQL مباشرة لايبلغHostB. لا ضماناتاتساقموزع أوتحديثفوريعبرالنسخ، ولااستنتاجصلاحياتمنCache.

UnitTests تختبرالتطبيعالتقني للمفتاح وTTL والحد والفشل والجيل والـimmutable snapshot؛ أضيفمرجعInfrastructure لمشروعUnitTests لاختبارالتنفيذمباشرة دونSQL، ولميتغيرمخططاعتمادالإنتاج. هذايحملحزمInfrastructure السابقة إلىgraph الاختبار فقط (منهاNewtonsoft13.0.4 بدل13.0.3)، دونترقيةحزمةإنتاج. Integration/HTTP تستخدمProgram الفعلي وSQL: لقطةقديمةتتوقفبعدSQL، ثمAdminCommit وGETجديدة وإكمالالقديمة، وكذلكفشلUPDATE إجباري ونسخةقديمة وحفظالقيمةالمخزنة. اختبارSeed يحميالإبطال بعدCommit ويثبتعدمإبطالrollback.
