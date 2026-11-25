# حزم geography في P03-S06

Reviewed: 2026-11-21

| الحزمة | الإصدار المثبت | الترخيص | سبب الاحتياج |
| --- | --- | --- | --- |
| [Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer.NetTopologySuite/10.0.14) | [10.0.14] direct فيInfrastructure | MIT | تحويل Point إلىSQL geography عبرمزود EF الحالي نفسه |
| [NetTopologySuite](https://www.nuget.org/packages/NetTopologySuite/2.6.0) | [2.6.0] direct فيInfrastructure | BSD-3-Clause | نوعPoint فيحد التخزين فقط |
| [NetTopologySuite.IO.SqlServerBytes](https://www.nuget.org/packages/NetTopologySuite.IO.SqlServerBytes/2.1.0) | 2.1.0 transitive ومقيد بالبصمة فيlockfiles | BSD-3-Clause | serialization المطلوب لمزود SQL/NTS |

راجعت صفحات NuGet الرسمية وlicense expression فيnuspec المستعاد لكلحزمة؛ تحفظ إشعارات الترخيص والنسب عندالتوزيع. لم يرقSDK10.0.401 أوMediatR12.5.0 أوحزم EF10.0.14 الحالية. مراجعة lockfiles تظهرالحزم الثلاث الجديدة وروابط المشروع فقط، دون تبديل إصدارات سابقة. locked restore بعدإنشاء graph لازم للتحقق المتكرر وCI.

[EF spatial](https://learn.microsoft.com/en-us/ef/core/modeling/spatial) يحدد UseNetTopologySuite وX=Longitude/Y=Latitude؛ حساب NTS العميلـي ليس قياس متر على الأرض. [SQL STDistance](https://learn.microsoft.com/en-us/sql/t-sql/spatial-geography/stdistance-geography-data-type?view=sql-server-ver17) يعمل بوحدة SRID ويقرب المسافة الجيوديسية؛4326 تستخدم المتر. اختباردرجة واحدة علىخطالاستواء يتوقع111319.49متر بسماحية300متر، ولا يدعي دقة موقع فرع أوتكامل خرائط.

GeoPoint لا تعرف NTS أوEF. HasGeographyPoint تطبيق تخزين اختياري فيInfrastructure.Shared.Geography؛ المستهلك المقرر فيROADMAP P04-S04، ولا كيان إنتاجي يضاف قبلتوقيته. لا Migration فيS06 إذ لم يضفحقل مطلوب لإنتاج. نموذج SpatialVerification وجدوله لايوجدان إلا فيIntegrationTests وقاعدتها المملوكة؛ النموذج الإنتاجي العادي يفحص ضد تسرب نوعالاختبار.
