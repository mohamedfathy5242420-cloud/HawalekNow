# حزم تأسيس CQRS والتسجيل

OwnerStep: P01-S04
Updated: 2026-10-11

كل PackageReference جديد يستخدم نطاقًا exact مثل `[12.5.0]`، دون ترقية MediatR. ملفات packages.lock.json في Application وApi تثبت dependency graph وcontent hashes؛ استخدم `dotnet restore backend/HawalekNow.slnx --locked-mode` للتحقق. الـSDK وnet10.0 محفوظان.

| الحزمة المباشرة | النسخة | الطبقة والغرض | الترخيص والمصدر الرسمي | التوافق |
| --- | --- | --- | --- | --- |
| MediatR | 12.5.0 | Application؛ Send وDI وBehaviors | Apache-2.0؛ [NuGet](https://www.nuget.org/packages/MediatR/12.5.0)، [ترخيص نفس tag](https://raw.githubusercontent.com/jbogard/MediatR/v12.5.0/LICENSE) | net6.0/netstandard2.0؛ net10.0 يستهلك net6.0 |
| FluentValidation | 12.1.1 | Application؛ async validation | Apache-2.0؛ [NuGet](https://www.nuget.org/packages/FluentValidation/12.1.1)، [ترخيص النسخة](https://raw.githubusercontent.com/FluentValidation/FluentValidation/12.1.1/License.txt) | net8.0 متوافق مع net10.0 |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | Application؛ فحص وتسجيل Validators | Apache-2.0؛ [NuGet](https://www.nuget.org/packages/FluentValidation.DependencyInjectionExtensions/12.1.1) | net8.0؛ يعتمد FV >=12.1.1 وDI abstractions |
| Microsoft.Extensions.Logging.Abstractions | 10.0.0 | Application؛ ILogger دون اعتماد على Serilog أو HTTP | MIT؛ [NuGet](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/10.0.0)، [ترخيص Microsoft](https://github.com/dotnet/runtime/blob/v10.0.0/LICENSE.TXT) | net10.0 متاح؛ DI abstractions 10.0.0 |
| Serilog | 4.3.0 | Api؛ إعداد structured logger وfilter | Apache-2.0؛ [NuGet](https://www.nuget.org/packages/Serilog/4.3.0) | net9.0 متوافق مع net10.0 |
| Serilog.Extensions.Hosting | 10.0.0 | Api؛ ربط ILogger بـSerilog وlifetime | Apache-2.0؛ [NuGet](https://www.nuget.org/packages/Serilog.Extensions.Hosting/10.0.0) | net10.0؛ يتوافق مع host .NET 10 |
| Serilog.Sinks.Console | 6.0.0 | Api؛ console فقط | Apache-2.0؛ [NuGet](https://www.nuget.org/packages/Serilog.Sinks.Console/6.0.0) | net8.0 متوافق مع net10.0؛ Serilog >=4.0.0 |

راجعت صفحات NuGet الرسمية وREADME MediatR لنفس tag وDI documentation لـFluentValidation؛ راجعت license expressions وtarget frameworks وdependencies في nuspec للحزم المستعادة من NuGet. Restore وBuild واختبارات DI تثبت التوافق العملي؛ توافق NuGet المحسوب وحده ليس اختبار Runtime. لا MediatR 13+ أو license key خاص بإصدارات أخرى. [تسجيل MediatR 12.5.0](https://github.com/jbogard/MediatR/tree/v12.5.0) و[تسجيل Validators](https://docs.fluentvalidation.net/en/latest/di.html).

الحزم الانتقالية الجديدة الفعلية: MediatR.Contracts 2.0.1 (Apache-2.0)، Microsoft.Extensions.DependencyInjection.Abstractions 10.0.0 (MIT)، Serilog.Extensions.Logging 10.0.0 (Apache-2.0). ترخيصها ومصدرها في NuGet: [Contracts](https://www.nuget.org/packages/MediatR.Contracts/2.0.1)، [DI](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions/10.0.0)، [Logging adapter](https://www.nuget.org/packages/Serilog.Extensions.Logging/10.0.0). ASP.NET/shared framework يوفر Microsoft.Extensions الأخرى في Web host؛ لا PackageReference لـASP.NET في Application. الحزم السابقة للاختبارات محفوظة دون تغيير.

لا FluentValidation.AspNetCore أو auto-validation؛ التحقق داخل async MediatR Pipeline. لا Serilog.AspNetCore أو request logging middleware أو configuration package أو file sink أو منصة تجميع؛ Hosting adapter وConsole هما الحد المطلوب. توسيع مخطط السجلات لاحقًا يحتاج مراجعة حماية البيانات.
