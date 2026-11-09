# تحقق P01 وبناء وتشغيل API داخل Docker

الـ[Workflow](../.github/workflows/p01-baseline.yml) يعمل على pull_request وpush إلى main وworkflow_dispatch. ثلاث Jobs مستقلة على GitHub-hosted Ubuntu 24.04؛ لا self-hosted أو خدمات جهاز المستخدم أو أسرار شخصية. permissions=contents:read وcheckout لا يحتفظ بالمصادقة، ولا Registry أو Deploy أو تغيير حماية الفروع. لا pull_request_target.

تم تنفيذ trigger الـPR فعليًا ونجح؛ push main لم ينفذ لأن الدمج غير مأذون، وworkflow_dispatch مُعرّف ولم يُشغّل. ظهور زر التشغيل اليدوي في واجهة GitHub يتطلب ملف Workflow في الفرع الافتراضي؛ المرجع يوضح أيضًا dispatch عبر API/CLI بعد تشغيل Workflow مرة واحدة على الأقل. انظر [توثيق الأحداث الرسمي](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_dispatch)؛ لا ندمج المرحلة لمجرد تفعيل الزر.

## SDK وActions

يقرأ SDK من [global.json](../global.json): 10.0.401. setup-dotnet يثبته في runner.temp المعزول وتتحقق المطابقة قبل Restore؛ لا تغيير rollForward في المشروع. Actions مثبتة على commits قرئت من tags الرسمية بتاريخ 2026-11-09:

| Action | النسخة | SHA والمصدر |
| --- | --- | --- |
| checkout | v7.0.1 | [3d3c42e5aac5ba805825da76410c181273ba90b1](https://github.com/actions/checkout/tree/3d3c42e5aac5ba805825da76410c181273ba90b1) |
| setup-dotnet | v6.0.0 | [a98b56852c35b8e3190ac28c8c2271da59106c68](https://github.com/actions/setup-dotnet/tree/a98b56852c35b8e3190ac28c8c2271da59106c68) |

تراخيص الـActions MIT؛ [checkout LICENSE](https://github.com/actions/checkout/blob/3d3c42e5aac5ba805825da76410c181273ba90b1/LICENSE)، [setup-dotnet LICENSE](https://github.com/actions/setup-dotnet/blob/a98b56852c35b8e3190ac28c8c2271da59106c68/LICENSE). سلوك global-json/install موثق في [المصدر الرسمي](https://github.com/actions/setup-dotnet). الحزم الحالية وتراخيصها في [PACKAGES](PACKAGES.md)، ولا ترقيات في S08.

## فحوص قابلة للتكرار من Clone نظيف

ثبت SDK المتوافق وGit وPowerShell 7؛ افتح PowerShell من جذر clone. لا يحتاج الحل SQL أو Mailpit حاليًا:

```powershell
dotnet --version
dotnet restore backend/HawalekNow.slnx --locked-mode
dotnet build backend/HawalekNow.slnx --configuration Release --no-restore
dotnet test backend/HawalekNow.slnx --configuration Release --no-build --no-restore
dotnet format backend/HawalekNow.slnx --verify-no-changes --no-restore
./planning/verify-roadmap.ps1
./planning/verify-git-workflow.ps1
./planning/test-phase-gates.ps1
git diff --check
dotnet run --project backend/src/HawalekNow.Api --configuration Release --no-build --no-restore --no-launch-profile -- --environment Development --urls http://127.0.0.1:5080
```

Package lockfiles تغطي كل مشاريع الحل بما فيها Unit/Architecture؛ Restore locked-mode يرفض تغير graph. CI لا يستخدم إصلاح Format أو تعطيل اختبارات/Assertions أو تعطيل توازي xUnit. فاحصا التوثيق يعملان عبر pwsh على Linux وWindows باستخدام Root المحسوب دون الاعتماد على مسار D:.

## الصور التي يبنيها CI

| الصورة | السياق وDockerfile | الغرض والحدود |
| --- | --- | --- |
| hawaleknow/api:p01-ci | جذر المستودع، [backend/Dockerfile](Dockerfile)، [.dockerignore](../.dockerignore) allowlist | multi-stage restore locked/publish، runtime غير root، Production افتراضي؛ smoke منفصل للحياة وCorrelation والبوابة |
| hawaleknow/sql-fts:p01-ci | docker/sql فقط، [Dockerfile الموجود](../docker/sql/Dockerfile) | نفس base digest وحزم SQL/FTS exact من S06؛ البناء لا يشغل SQL أو يثبت persistence |
| Mailpit | لا تبنى؛ upstream digest موجود في Compose | فحوص SMTP/الاستمرار تاريخية في S06، لا تعتمد CI على بريد أو قاعدة محلية |

SDK image=10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317، runtime ASP.NET=10.0.12@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4. قرئت manifests من MCR الرسمي؛ Dockerfile يفحص SDK الفعلي. صور Microsoft الرسمية وتراخيص مكوناتها في [dotnet-docker](https://github.com/dotnet/dotnet-docker)، [SDK](https://github.com/dotnet/dotnet-docker/blob/main/README.sdk.md)، [ASP.NET](https://github.com/dotnet/dotnet-docker/blob/main/README.aspnet.md). لا ادعاء reproducibility بايت ببايت لحزم نظام التشغيل.

```powershell
docker build --pull --tag hawaleknow/api:p01-ci --file backend/Dockerfile .
./docker/smoke-api.ps1 -Image hawaleknow/api:p01-ci
docker build --pull --platform linux/amd64 --tag hawaleknow/sql-fts:p01-ci docker/sql
docker run --detach --name hawaleknow-api-review --cpus=1 --memory=256m --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m --publish 127.0.0.1:5082:8080 --env ASPNETCORE_ENVIRONMENT=Development hawaleknow/api:p01-ci
```

الـsmoke ينشئ اسمًا عشوائيًا خاصًا بالمهمة ومنفذ loopback عشوائيًا، بلا mounts أو volumes أو credentials؛ يفحص Production liveness و404 آمنة للتوثيق/Readiness والمستخدم غير root، ثم يزيل حاويته وحدها. التشغيل اليدوي الأخير على 5082 يفتح [Scalar Docker](http://127.0.0.1:5082/scalar/) و[JSON Docker](http://127.0.0.1:5082/openapi/v1.json). لا يحتاج SQL/بريد، ولا يثبت جاهزية Production. لا .env أو Git أو local settings أو مخرجات build سابقة داخل السياق أو الصورة؛ لا artifacts uploads في workflow.

## التنظيف الآمن والخدمات السابقة

للتشغيل اليدوي أعلاه فقط، تحقق من هوية حاوية المراجعة قبل إزالتها:

```powershell
docker ps -a --filter name=hawaleknow-api-review --format '{{.Names}} {{.Image}} {{.Status}}'
docker rm --force hawaleknow-api-review
```

لا prune أو down -v أو حذف Volumes أو تغيير خدمات قائمة. أوامر SQL/Mailpit وbootstrap والأسرار المحلية في [Docker README](../docker/README.md)؛ تلك الموارد متوقفة وقت مراجعة P01 الحالية ولم تُعد تشغيلها. الأدلة التاريخية للاستمرار تبقى في S06، والفحص الحالي للقراءة فقط لا يعيد اختبارها. [PHASE_REVIEW](../planning/phase-01/PHASE_REVIEW.md) يسجل النتائج الحالية وCI run والقيود والمراجعات البشرية Pending.
