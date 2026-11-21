# تشغيل البيانات المرجعية P03

لا تستورد Seed تلقائيًا عند بدء HTTP. راجع ConnectionStrings:Application في بيئتك الآمنة، وطبّق Migrations الإضافية على القاعدة المقصودة قبل الاستيراد. لا تستخدم قاعدة التطبيق لاختبارات التدمير، ولا تعِد إنشاء قاعدة أو volume.

```powershell
dotnet restore backend/HawalekNow.slnx --locked-mode
dotnet build backend/HawalekNow.slnx --configuration Release --no-restore
# بعد اختيار الاتصال الصحيح في إعداداتك المحلية الآمنة:
dotnet tool run dotnet-ef database update --project backend/src/HawalekNow.Infrastructure --configuration Release
dotnet backend/src/HawalekNow.Api/bin/Release/net10.0/HawalekNow.Api.dll --seed-reference-data
```

لا تضع الاتصال أو كلمات المرور في المستودع أو سطر الأوامر. الأمر لا يحتاج بيانات Admin ولا ينشئ حسابات أو يشغل HTTP/Jobs. لا تجمعه مع --seed-admin. فشل التحقق أو الكتابة يعيد exit1 برسالة ثابتة دون أسرار؛ النجاح يطبع أعداد الإضافات بعد Commit.

المصدر مثبت ومضمّن؛ لا تنزيل وقت التشغيل. [المصدر والترخيص والبصمات](src/HawalekNow.Infrastructure/Modules/Locations/SeedData/SOURCE.md): D019، 27 محافظة، 365 قسمًا/مركزًا، 5716 شياخة. 14 قسمًا بلا مناطق، و18 مجموعة أسماء إنجليزية متكررة صحيحة محفوظة. هذه تغطية النسخة المعتمدة، وليست إثبات اكتمال مصر.

الأنشطة الأربعة من المتطلبات: ملابس وإكسسوارات، إلكترونيات وموبايلات، أثاث وأدوات منزلية، قطع غيار سيارات؛ الإنجليزية ترجمة عرض. SourceKey ثابت وسجل استيراد دائم لكل عنصر؛ إعادة التشغيل لا تكرر صفًا ولا تغير الأسماء أو التفعيل أو النقل أو تعيد عنصرًا حذفه Admin. تطابق اسمي نشاط موجود قبل أول استيراد يسمح بتبنيه دون تغيير حالته؛ تعارض أحدهما يفشل الاستيراد كله. لا تمسح سجلات SeedKeys لإجبار إعادة الاستيراد.

معاملة واحدة تشمل الوحدتين عبر عقد Catalog العام، بقفل Catalog ثم Locations. يحفظ الآباء ثم الأبناء، وCommit واحد بعد الجميع. لا تنفذ Seed في عدة Hosts دون مراجعة اتساق الذاكرة المحلية الموثق عند اكتمال S07. للمراجعة الآلية المعزولة:

```powershell
./backend/test-p02.ps1 -NoBuild -Filter FullyQualifiedName~ReferenceSeed
```

ينشئ الاختبار قواعد HN_P02_Test المملوكة له وينظفها فقط؛ لا يستورد في قاعدة التطبيق. مراجعة المستخدم وV4 وجلسة الشرح منفصلة عن أدلة الاختبارات.
