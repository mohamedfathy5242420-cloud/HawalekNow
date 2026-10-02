## ملخص التغيير والمخرج

تجهيز Git workflow قبل P01: main مستقر، فرع قصير لكل Step، Conventional Commits بمحتوى واضح ومعرف الخطوة، PR واحد عادةً، منع force push على main، ومراجعة الفرق وتسجيل الفحوص الفعلية.
إضافة .gitignore لـ.NET وAngular وDocker والتطوير والأسرار مع السماح بأمثلة البيئة وlockfiles وmigrations، وقالب PR. تحديث فاحص الخريطة لانتقال S04 مع الحفاظ على مطابقة 76 خطوة و75 اعتمادًا وستة قرارات Approved. توثيق baseline واستبعاد مواد المصدر والاستخراج محليًا دون حذفها.

## معرف الخطوة

Step: P00-S04
Step file: planning/phase-00/steps/S04.md
Handoff: planning/phase-00/handoffs/S04-to-P01.md — Draft

## أدلة التحقق

V1: verify-roadmap.ps1 وverify-git-workflow.ps1 وgit diff --check وgit diff --cached --check؛ النتائج النهائية في S04.
V2: N/A؛ توثيق وحوكمة فقط.
V3: baseline efcb6a5 مرفوعة؛ نتيجة Push الفرع وPR تسجل في S04.
V4: Awaiting user review.
لا CI أو Build للتطبيق في هذه المهمة.

## القرارات المتأثرة

D008 يطبق بقواعد Git المفصلة؛ D001–D008 وQ003-01 إلى Q003-06 لم تتغير. لا Backend أو بدء P01. الخطوة InReview وتسليم المرحلة Draft؛ لا دمج ضمن هذه المهمة.

## مراجعة قبل الدمج

- [x] مراجعة الملفات المختارة وقواعد الاستبعاد.
- [x] توثيق حدود التحقق والمراجعة البشرية.
- [ ] اعتماد المستخدم وإغلاق P00؛ لم يتم بعد.
- [ ] الدمج؛ ممنوع في مهمة S04 الحالية.
