# Git workflow في HawalekNow

Updated: 2026-11-10
OwnerStep: P00-S04
Status: Approved

## القواعد

main هو الفرع المستقر. وفق D012 تجمع المرحلة المأذونة في فرع `codex/` وDraft PR يشرحان نطاقها. يبدأ الفرع من main المحدث بعد بوابة المرحلة السابقة؛ تنفذ الخطوات الداخلية بالتتابع بعد نجاح تحقق السابقة. تحتفظ P01 بالفرع codex/p01-s07-document-and-version-api وPR #8 الموجودين لحفظ المراجعة والتاريخ. أمثلة Step والفروع القديمة أدناه سجل لطريقة التنفيذ السابقة.

رسائل Commit بأسلوب Conventional Commits: `type(scope): meaningful change [Pxx-Syy]`. مثال: `chore(repo): configure git workflow and pull request template [P00-S04]`. كل Commit يشرح التغيير الفعلي؛ معرف الخطوة وحده أو update/fixes/finish-step غير كافٍ. الأنواع المعتادة docs وchore وfeat وfix وtest.

PR واحد للمرحلة إلى main يجمع مخرجات الخطوات وأدلة V1–V4 والقرارات والقيود، ويربط PHASE_REVIEW وملفات الخطوات وHandoff ويستخدم [القالب](../.github/pull_request_template.md). أكثر من Commit مسموح داخل نفس PR عند وجود تغييرات مميزة؛ تبقى معرفات الخطوات في رسائلها.

راجع Diff قبل كل Commit وPush والدمج، بما يشمل الملفات الجديدة والأسرار والملفات الثنائية. استخدم `git add -- <paths>` لقائمة مختارة بدل الإضافة بالجملة. لا تستخدم force push على main، ولا تعِد كتابة تاريخ موجود. إذا ظهر تاريخ بعيد غير متوقع، توقف عن الرفع وافحصه ثم حافظ عليه بدمج مدروس؛ لا تستبدله بتاريخ محلي.

سجل المرحلة InProgress عند البدء، ثم InReview عند جاهزية مراجعتها. تميز الخطوة ExecutionStatus وVerificationStatus عن UserApproval وV4؛ InternalReady مسموح للتسليم داخل المرحلة بعد التحقق. لا تسجل Done أو تسليم المرحلة التالية Ready قبل اعتماد المستخدم المطلوب. سجل الفحوص الفعلية وسبب N/A والفشل والعائق؛ وجود PR أو نجاح Push أو CI لا يعني اعتماد المرحلة. لا دمج أو تغيير Branch Protection أو تكاملات في إذن تنفيذ P01 الحالي.

## إذن إغلاق P01 الحالي

وفق D013 بتاريخ 2026-11-10 أذن المستخدم Commit/Push على فرع PR #8 الموجود ثم Ready for Review وSquash Merge بعد إعادة نجاح فحوص الإغلاق وCI على الرأس الفعلي وفحص الحماية وقابلية الدمج. هذا يستبدل منع الدمج في إذن التنفيذ السابق أعلاه لـP01 فقط؛ لا تغيير حماية أو force push أو حذف فرع. main يحدث بأمان مع فحوص بعد الدمج وCI main. P02 NotStarted وتسليمها Ready؛ لا إذن تنفيذها. التجارب الشخصية والتعليمية Deferred بقرار القبول وليست Passed. ملف TARGET_SCHEDULE.md بلا تعديل أو staging.

## مثال التنفيذ

من جذر الريبو، بعد مراجعة الحالة والاعتمادات:

```powershell
git status --short --branch
git remote -v
git ls-remote --symref origin
git fetch origin
git switch main
git pull --ff-only origin main
git switch -c chore/p00-s04-setup-git-workflow
# نفذ الخطوة ثم راجع قائمة الملفات والفرق
git diff --check
git diff
git add -- docs/GIT_WORKFLOW.md .github/pull_request_template.md .gitignore
git diff --cached --check
git diff --cached
git commit -m "chore(repo): configure git workflow and pull request template [P00-S04]"
git show --stat HEAD
git log --oneline origin/main..HEAD
git diff origin/main...HEAD
git push -u origin chore/p00-s04-setup-git-workflow
```

في هذا الجهاز Git احتاج `-c safe.directory=D:/HwalekNow` لكل أمر بسبب اختلاف الملكية؛ لم نغير إعداد safe.directory العام. افتح PR من GitHub واختر base: main وcompare: فرع الخطوة. راجع Files changed والـCommits. منع الدمج في تنفيذ S04 الأول كان حتى مراجعة المستخدم؛ بتاريخ 2026-10-04 اعتمد المستخدم المخرج وأذن بإغلاق P00 وSquash Merge لـPR #1. يجمع Squash تغييرات الـPR في Commit واحد على main؛ يحتفظ بفرع العمل، ولا يبدأ P01 بهذا الدمج. لا توجد CI مؤكدة حاليًا؛ GitHub Actions ستنشأ في خطوتها بالخطة.

## مراجعة ملفات البداية

قبل أول Commit فُحصت قائمة الملفات والتوثيق وWord XML وهوية origin؛ المحلي بلا Commits والبعيد بلا refs عند `git ls-remote origin` (exit 0). baseline تضم 16 ملفًا محددًا بالاسم، وفحص الفرق المتجه للـCommit نجح قبل إنشائه. رُفعت على main بالـCommit `efcb6a5`، وبعدها أنشئ فرع S04. لم يُحذف أي ملف مستخدم.

| الملفات | القرار والسبب |
| --- | --- |
| Hawalek_Now_Approved_Requirements_AR.docx | تتبع؛ المرجع المعتمد للمتطلبات |
| Hawalek_Now_Backend_Master_Execution_Plan_AR.docx | تتبع؛ مصدر 76 خطوة والفاحص يحتاجه؛ لم يعدل |
| docs والحالة والقرارات والنطاق؛ ROADMAP والقوالب وS01–S03 وتسليماتها والفاحص | تتبع؛ سجل التنفيذ الحالي ومدخل الاستكمال |
| planning/phase-00/handoffs/P00-S03-REVIEW-REPORT.md | تتبع؛ تقرير طلبه المستخدم وسجل مراجعة تاريخي موضح بتنبيه؛ ليس تقريرًا مؤقتًا، والنسخ المضمنة فيه تاريخية |
| Mazen_Source/ بما فيها ZIP والصور وPDF وOffice | استبعاد محلي؛ مواد بحث خام ومكررة وليست مدخل build؛ المرجع الموحد المعتمد كافٍ للتنفيذ؛ لا نشر للمرفقات الخام دون مراجعة منفصلة |
| Hawalek_Now_Project_Questions_AR_Answered.docx | استبعاد محلي؛ مرحلة مناقشة سابقة يلخصها المرجع المعتمد وسجل القرارات، لتجنب مراجع متنافسة |
| extract_project_materials.py | استبعاد محلي؛ أداة استخراج لمرة واحدة مرتبطة بمسار الجهاز وبمواد المصدر المستبعدة، وليست أداة تشغيل المشروع |
| project_materials_extracted.json | استبعاد محلي؛ مخرج مشتق يمكن إعادة توليده وليس مرجعًا معتمدًا |
| tmp/temp/artifacts وLogs وTestResults وcoverage | استبعاد؛ مخرجات مؤقتة أو مولدة؛ أدلة التحقق الدائمة تُلخص في ملفات الخطوة والتسليم |

## الاستبعادات وحماية الملفات اللازمة

[.gitignore](../.gitignore) يستبعد bin/obj وnode_modules وdist وذاكرة Angular وبيانات Docker المحلية وإعدادات IDE وملفات التطوير المحلية والأسرار. Dockerfile وcompose وmigrations وملفات الحل والمشاريع وlockfiles غير مستبعدة. `.env.example` و`.env.*.example` مسموح تتبعها بمحتوى placeholders فقط. ملفات appsettings الأساسية ليست مستبعدة؛ راجع محتواها قبل الإضافة واجعل الأسرار في إعداد محلي أو secret store. ملف .gitignore لا يزيل ملفًا سبق تتبعه؛ راجع `git ls-files` أيضًا.

يشغل [فحص قواعد Git](../planning/verify-git-workflow.ps1) عينات لمسارات ممنوعة ومسموحة باستخدام `git check-ignore --no-index` دون إنشاء ملفات أسرار أو build. حجب امتداد شهادة أو بيانات محلية استبعاد احترازي؛ إن احتاج المشروع شهادة عامة أو إعداد IDE مشترك، تُراجع إضافته المحددة أولًا.
