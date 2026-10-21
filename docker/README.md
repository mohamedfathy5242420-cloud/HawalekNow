# خدمات التطوير المحلية — P01-S06

الخدمتان فقط: SQL Server 2025 مع Full Text Search وMailpit. مفيش تطبيق داخل Docker ولا EF/Migrations أو OTP أو Brevo في الخطوة دي. [سجل التنفيذ والنتائج](../planning/phase-01/steps/S06.md).

## Docker ببساطة

- **Image**: قالب فيه نظام ملفات الخدمة وبرنامجها؛ زي نسخة ثابتة من تجهيز SQL. الـtag اسم إصدار، والـdigest بصمة المحتوى اللي بتثبت الصورة نفسها.
- **Container**: عملية شغالة من القالب، ليها إعدادات وشبكة وطبقة ملفات قابلة للتغيير. نقدر نستبدلها لما نغير الإعداد.
- **Volume**: مساحة تخزين Docker بيديرها بعيد عن عمر الـContainer. ده مش Backup؛ لو القرص أو الـVolume ضاع، البيانات ممكن تضيع.
- **Compose**: ملف بيقول لـDocker يشغل أنهي خدمات، بمنافذ وإعدادات وتخزين وفحوص صحة إيه. اسم المشروع هنا `hawaleknow-local`؛ ما تغيّروش عشان ما تبدأش Volumes مختلفة بالخطأ.
- **Health Check**: أمر دوري بيختبر الاستجابة. Running معناها العملية شغالة؛ Healthy معناها آخر فحص نجح. SQL هنا بيجرب تسجيل دخول واستعلام ووجود FTS؛ Mailpit بيشغل `/mailpit readyz`. اختبار فهرسة Full Text الكامل منفصل.

## المتطلبات والمصادر والصور

Docker Engine شغال في وضع Linux على Intel/AMD x64، وCompose يدعم `up --wait`. وجود docker.exe لوحده مش كفاية. SQL محتاج على الأقل 2 GB ذاكرة؛ سيب مساحة للخدمة والنظام وMailpit. البناء والصور محتاجين عدة GB ومساحة إضافية للبيانات. الجهاز المفحوص: 12 CPU و3.64 GiB للـEngine؛ النتائج التفصيلية في S06. مفيش تغيير تلقائي لإعدادات الجهاز؛ لو Engine مش شغال افتح Docker Desktop يدويًا وانتظر جاهزيته.

| المكون | المرجع المثبت | الغرض |
| --- | --- | --- |
| SQL base | `mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-22.04@sha256:9f07099847fb885aba664fc1f1c52f7a2eea317a1ae8fd4519f7a716823f7c0e` | SQL 17.0.5015.4، Ubuntu 22.04، amd64، sqlcmd18 |
| حزم SQL وFTS | `mssql-server=17.0.5015.4-1` و`mssql-server-fts=17.0.5015.4-1` | نفس build للمحرك والبحث |
| الصورة المحلية المشتقة | `hawaleknow/sql-fts:17.0.5015.4-s06` | يبنيها [Dockerfile](sql/Dockerfile) من المرجع أعلاه |
| Mailpit | `axllent/mailpit:v1.31.6@sha256:23fe39e81bde596a918000b3135c0f85dc8bb4c9ea6be274b051143f3e300387` | SMTP محلي وواجهة/API وSQLite مستمر |

Microsoft بتوثق FTS كحزمة مستقلة. الصورة الأساسية فيها المحرك منسوخ من غير سجل dpkg للمحرك، ومفيهاش ملفات FTS المطلوبة. الصورة المشتقة بتثبت الحزمتين بنفس الإصدار بدل ما اعتماد FTS يختار محرك أحدث. البناء يستخدم مستودع Microsoft الموقّع ومفاتيح الثقة الموجودة في الصورة، ولا يعطل توقيع APT؛ يرجع للمستخدم `mssql` بعد التثبيت. ما بننسخش أسرار للبناء؛ سياقه مجلد sql فقط و.dockerignore بيحصره في Dockerfile. تثبيت base بالـdigest والحزمتين exact بيمنع الترقية الصامتة؛ مستودعات نظام التشغيل ليست snapshot، فلا ندعي إن إعادة بناء المستقبل هتنتج نفس Image ID بايت ببايت. أي تحديث للصورة أو الحزمة محتاج إعادة اختبار SQL وFTS.

المصادر الرسمية: [صور Microsoft ونشرها](https://learn.microsoft.com/en-us/sql/linux/containers/deploy?view=sql-server-ver17)، [تثبيت FTS واللغات](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/setup-full-text-search?view=sql-server-ver17)، [SQL CU9+GDR والإصدار](https://support.microsoft.com/en-us/servicing/sql/kb321185-download-and-install-latest-updates)، [مستودع الحزمة](https://packages.microsoft.com/config/ubuntu/22.04/mssql-server-2025.list)، [صور Mailpit الرسمية](https://mailpit.axllent.org/docs/install/docker/)، [خيارات التخزين](https://mailpit.axllent.org/docs/configuration/runtime-options/)، [مصدر فحص Mailpit](https://github.com/axllent/mailpit/blob/v1.31.6/Dockerfile).

التراخيص: `MSSQL_PID=EnterpriseDeveloper` و`ACCEPT_EULA=Y` لاستخدام تطوير واختبار وفق [نسخ SQL 2025 الرسمية](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-editions-and-components-2025?view=sql-server-ver17). ده مش ترخيص Production. Mailpit بترخيص [MIT لنفس النسخة](https://github.com/axllent/mailpit/blob/v1.31.6/LICENSE). استخدام Docker Desktop يخضع [لشروطه](https://docs.docker.com/subscription-billing/desktop-license)، وفيه شروط مختلفة للاستخدام الشخصي والمنظمات؛ الخطوة لم تغير التثبيت أو الاشتراك.

## التشغيل خطوة بخطوة

من جذر المستودع في PowerShell. ابدأ بفحص Engine وCompose؛ `OSType` لازم يكون linux، وأي Server error يوقف التشغيل لحين حل الجهاز يدويًا:

```powershell
docker version
docker compose version
docker info --format 'OSType={{.OSType}} Architecture={{.Architecture}} CPUs={{.NCPU}} MemoryBytes={{.MemTotal}}'
Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object LocalPort -in 1433,1025,8025 | Select-Object LocalAddress,LocalPort
```

1. ولّد كلمة مرور عشوائية محلية مرة واحدة. `Init` لا يكتب فوق ملف موجود. بديل يدوي: جهز `.env` من [.env.example](.env.example) بسطر MSSQL_SA_PASSWORD فقط واستبدل المثال بكلمة عشوائية قوية. wrapper يقبل 16–128 حرفًا من A-Z/a-z/0-9 والرموز `!_@#%+-`، مع حرف كبير وصغير ورقم ورمز. لا `$` أو مسافات لتجنب interpolation. `.env` مستبعد من Git؛ لا تطبعه أو تضيفه بالقوة. المستخدمون اللي يقدروا يقرأوا الملف أو يدخلوا Docker Engine يقدروا يصلوا للسر، فده إعداد محلي للتطوير.

```powershell
./docker/local.ps1 Init
./docker/local.ps1 Validate
```

2. `Validate` يستخدم `compose config --quiet`؛ ما بيطبعش الإعداد الموسّع. `Up` بيبني الصورة ويشغل الخدمتين، وينتظر حتى 240 ثانية لنجاح Health Checks. أول بناء بيحمل حزم كبيرة وممكن ياخد وقت.

```powershell
./docker/local.ps1 Up
./docker/local.ps1 Status
```

3. افتح [Mailpit](http://127.0.0.1:8025). SQL للاتصال المحلي: `127.0.0.1,1433`، SQL authentication بحساب `sa` وكلمة المرور من ملفك، مع trust server certificate للتجربة المحلية. الـsqlcmd المستخدم داخل Container يثق في شهادة SQL المحلية بـ`-C`. ده مش إعداد TLS إنتاج. SMTP على `127.0.0.1:1025` بدون authentication/TLS للتطوير؛ Mailpit يلتقط البريد ولا يوجد relay أو forwarding خارجي مُعد. كل published ports مربوطة بـ127.0.0.1 فقط؛ الخدمات تقدر تتواصل داخل شبكة Compose الخاصة بالمشروع.

| الخدمة | منفذ الجهاز | Volume | موضع البيانات داخل Container |
| --- | --- | --- | --- |
| SQL | 127.0.0.1:1433 | hawaleknow-local_sql-data | /var/opt/mssql؛ ملفات البيانات والـlog وsystem databases |
| Mailpit SMTP | 127.0.0.1:1025 | hawaleknow-local_mailpit-data | /data/mailpit.db مع ملفات SQLite الجانبية |
| Mailpit UI/API | 127.0.0.1:8025 | نفس Volume البريد | نفس قاعدة البريد |

الـVolumes موجودة داخل تخزين Docker Desktop على Linux، مش فولدر المشروع في D:. تركيب نفس Volume على `/var/opt/mssql` بعد إعادة الإنشاء بيرجع ملفات SQL القديمة. تغيير كلمة مرور `.env` بعد تأسيس SQL **لا يغير** كلمة مرور sa المخزنة في قاعدة SQL؛ عدم التطابق يفشل Health Check. احتفظ بالقيمة المحلية أو غيّرها بإجراء SQL مقصود ومراجَع، من غير حذف البيانات.

## الفحص الفعلي واستمرار البيانات

```powershell
./docker/test-settings.ps1
./docker/verify-services.ps1
./docker/verify-services.ps1 -Persistence
```

الأمر الأول يختبر غياب/سوء الإعداد وضعف/placeholder لكلمة المرور وعدم تسريبها وعدم الكتابة فوقها. الثاني ينشئ قاعدة اختبار جديدة `S06Probe_<guid>` لا تخص التطبيق؛ يرفض أي اسم موجود، ينشئ جدولًا وصفين وفهرس Full Text باللغة الإنجليزية، وينتظر حتى 120 ثانية ثم يتأكد أن CONTAINS يرجّع الصف المتوقع فقط. يرسل رسالة SMTP غير حساسة ويثبت استقبالها ومحتواها عبر API. يحفظ receipt محليًا في tmp مع اسم القاعدة ومعرف الرسالة والـmarker، بلا كلمة مرور. لو receipt موجود استخدم -Persistence أو اسم ReceiptFile جديد؛ لا يعدّل قاعدة قديمة تلقائيًا.

`-Persistence` يقرأ receipt، يفحص البيانات (ينتظر جاهزية استعلام SQL حتى 120 ثانية، مع حد 15 ثانية للاتصال و10 للاستعلام في كل محاولة؛ المحاولة الجارية قد تنتهي بعد حد الحلقة)، يعيد تشغيل الخدمتين وينتظر صحتهما ويفحص البيانات، ثم يعيد إنشاء الاثنين ويتأكد أن IDs اتغيرت وأن SQL والبريد موجودان. العملية توقف الخدمات لحظيًا. الاختبار يثبت الأساس التقني، مش تصميم بحث المنتج أو جودة تقطيع الكلمات العربية أو OTP أو تخزين التطبيق في P02. إعادة الفهرسة وتغييرات اللغات في SQL 2025 مذكورة في مصدر Microsoft أعلاه وتراجع وقت P06.

## الإيقاف وإعادة الإنشاء

```powershell
./docker/local.ps1 Stop       # يوقف العمليتين؛ Containers وVolumes باقية
./docker/local.ps1 Up         # يرجعهم وينتظر الصحة
./docker/local.ps1 Recreate   # Containers جديدة بنفس Volumes؛ توقف قصير
./docker/local.ps1 Down       # يزيل Containers وشبكة المشروع فقط؛ Volumes باقية
```

لا تستخدم `down -v` أو `docker system prune` أو تنظيف واسع. السكربتات لا تحذف بيانات الاختبار تلقائيًا؛ القاعدة والرسالة والـVolumes متروكين للمراجعة، وأي تنظيف لاحق يحدد اسم المورد ومعرفه وملكيته قبل التنفيذ. ما تغيرش اسم المشروع أو أسماء Volumes لو عايز ترجع لنفس البيانات.

## عند مشكلة

Missing local settings: شغّل Init مرة واحدة. Invalid settings/password: راجع سطر الإعداد والقيود بدون طباعة القيمة. Port allocated: اعرف صاحب المنفذ ووقف هنا بدل إيقاف خدمة تانية تلقائيًا. Pipe permission denied جوه العزل: اطلب صلاحية الأداة؛ لو Engine نفسه غير جاهز افتح Desktop يدويًا. Health timeout: راجع موارد Docker وتطابق كلمة المرور وإصدار الحزمة؛ لا تعتبر Running نجاحًا ولا تمسح الـVolume لإخفاء المشكلة. لا تطبع `docker compose config` كاملًا أو `docker inspect` كاملًا؛ كليهما قد يعرض السر. فحص health فقط آمن مثل `docker inspect --format '{{.State.Health.Status}}' <container-id>`.

بيئة Production محتاجة تصميم مستقل: إدارة أسرار وصلاحيات SQL محدودة بدل sa، شبكة وTLS وهوية وصول، نسخ احتياطي واختبار استرجاع ومراقبة وتحديثات وسعة وترخيص نشر. ربط loopback وVolumes محلية وMailpit لا يحقق المتطلبات دي.
