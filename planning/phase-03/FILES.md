# فهرس تغييرات P03

Updated: 2026-11-21
Status: InProgress

المقارنة من main المدمجة 5e4250ff31f84c8806649c7ef1786574da7dd00a وتشمل الملفات الموجودة فعليًا في العمل الجاري. وجود ملف لا يعني نجاح التحقق أو اعتماد المرحلة؛ راجع الخطوات وverification.json. لا تعديلات Word أو جدول المستخدم أو كود P04.

| الملف | المسؤولية |
| --- | --- |
| [.github/workflows/p01-baseline.yml](../../.github/workflows/p01-baseline.yml) | توثيق وحوكمة وأدلة |
| [backend/API_EXPLORATION.md](../../backend/API_EXPLORATION.md) | توثيق وحوكمة وأدلة |
| [backend/CI.md](../../backend/CI.md) | توثيق وحوكمة وأدلة |
| [backend/MODULES.md](../../backend/MODULES.md) | توثيق وحوكمة وأدلة |
| [backend/postman/HawalekNow.P03.Local.postman_environment.json](../../backend/postman/HawalekNow.P03.Local.postman_environment.json) | Collection وEnvironment بلا أسرار |
| [backend/postman/HawalekNow.P03.postman_collection.json](../../backend/postman/HawalekNow.P03.postman_collection.json) | Collection وEnvironment بلا أسرار |
| [backend/src/HawalekNow.Api/Modules/Catalog/CatalogControllers.cs](../../backend/src/HawalekNow.Api/Modules/Catalog/CatalogControllers.cs) | HTTP والأمن والتوثيق |
| [backend/src/HawalekNow.Api/Modules/Locations/LocationControllers.cs](../../backend/src/HawalekNow.Api/Modules/Locations/LocationControllers.cs) | HTTP والأمن والتوثيق |
| [backend/src/HawalekNow.Api/Program.cs](../../backend/src/HawalekNow.Api/Program.cs) | HTTP والأمن والتوثيق |
| [backend/src/HawalekNow.Api/Shared/OpenApi/ApiDocumentTransformer.cs](../../backend/src/HawalekNow.Api/Shared/OpenApi/ApiDocumentTransformer.cs) | HTTP والأمن والتوثيق |
| [backend/src/HawalekNow.Api/Shared/Security/ReferenceDataRateLimits.cs](../../backend/src/HawalekNow.Api/Shared/Security/ReferenceDataRateLimits.cs) | HTTP والأمن والتوثيق |
| [backend/src/HawalekNow.Application/Modules/Catalog/CatalogRequests.cs](../../backend/src/HawalekNow.Application/Modules/Catalog/CatalogRequests.cs) | عقود CQRS وDTO والتحقق |
| [backend/src/HawalekNow.Application/Modules/Locations/LocationRequests.cs](../../backend/src/HawalekNow.Application/Modules/Locations/LocationRequests.cs) | عقود CQRS وDTO والتحقق |
| [backend/src/HawalekNow.Domain/Modules/Catalog/Activity.cs](../../backend/src/HawalekNow.Domain/Modules/Catalog/Activity.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Domain/Modules/Catalog/CatalogNames.cs](../../backend/src/HawalekNow.Domain/Modules/Catalog/CatalogNames.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Domain/Modules/Catalog/Category.cs](../../backend/src/HawalekNow.Domain/Modules/Catalog/Category.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Domain/Modules/Catalog/CategoryPath.cs](../../backend/src/HawalekNow.Domain/Modules/Catalog/CategoryPath.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Domain/Modules/Locations/AdministrativePlaces.cs](../../backend/src/HawalekNow.Domain/Modules/Locations/AdministrativePlaces.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogAdministration.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogAdministration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogConfiguration.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogConfiguration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogQueries.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogQueries.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogRegistration.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogRegistration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogTreeLock.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogTreeLock.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationAdministration.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationAdministration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationConfiguration.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationConfiguration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationQueries.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationQueries.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationRegistration.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationRegistration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationStateLock.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationStateLock.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121111950_P03CatalogFoundation.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121111950_P03CatalogFoundation.cs) | Migration إضافية أو snapshot |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121111950_P03CatalogFoundation.Designer.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121111950_P03CatalogFoundation.Designer.cs) | Migration إضافية أو snapshot |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121121520_P03LocationsHierarchy.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121121520_P03LocationsHierarchy.cs) | Migration إضافية أو snapshot |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121121520_P03LocationsHierarchy.Designer.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121121520_P03LocationsHierarchy.Designer.cs) | Migration إضافية أو snapshot |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/HawalekDbContextModelSnapshot.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/HawalekDbContextModelSnapshot.cs) | Migration إضافية أو snapshot |
| [backend/tests/IntegrationTests/ApiFoundationTests.cs](../../backend/tests/IntegrationTests/ApiFoundationTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/CatalogConcurrencyTests.cs](../../backend/tests/IntegrationTests/CatalogConcurrencyTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/CatalogHttpTests.cs](../../backend/tests/IntegrationTests/CatalogHttpTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/CatalogPersistenceTests.cs](../../backend/tests/IntegrationTests/CatalogPersistenceTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/FoundationCatalogPorts.cs](../../backend/tests/IntegrationTests/FoundationCatalogPorts.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/FoundationIdentityPorts.cs](../../backend/tests/IntegrationTests/FoundationIdentityPorts.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/FoundationLocationPorts.cs](../../backend/tests/IntegrationTests/FoundationLocationPorts.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/LocationHttpTests.cs](../../backend/tests/IntegrationTests/LocationHttpTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/LocationPersistenceTests.cs](../../backend/tests/IntegrationTests/LocationPersistenceTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/UnitTests/CatalogModelTests.cs](../../backend/tests/UnitTests/CatalogModelTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/UnitTests/CategoryPathTests.cs](../../backend/tests/UnitTests/CategoryPathTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/UnitTests/LocationModelTests.cs](../../backend/tests/UnitTests/LocationModelTests.cs) | اختبارات وفواصل عزل |
| [docs/CURRENT_STATE.md](../../docs/CURRENT_STATE.md) | توثيق وحوكمة وأدلة |
| [docs/DECISIONS.md](../../docs/DECISIONS.md) | توثيق وحوكمة وأدلة |
| [docs/P02_CLOSED_STATE.md](../../docs/P02_CLOSED_STATE.md) | توثيق وحوكمة وأدلة |
| [docs/P03_SOURCE_REVIEW.md](../../docs/P03_SOURCE_REVIEW.md) | توثيق وحوكمة وأدلة |
| [planning/phase-02/MERGE_RECEIPT.md](../phase-02/MERGE_RECEIPT.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/DESIGN.md](DESIGN.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/handoffs/S01-to-S02.md](handoffs/S01-to-S02.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/handoffs/S02-to-S03.md](handoffs/S02-to-S03.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/handoffs/S03-to-S04.md](handoffs/S03-to-S04.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/steps/S01.md](steps/S01.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/steps/S02.md](steps/S02.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/steps/S03.md](steps/S03.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/verification.json](verification.json) | توثيق وحوكمة وأدلة |
| [planning/ROADMAP.md](../ROADMAP.md) | توثيق وحوكمة وأدلة |
| [planning/test-p02-gates.ps1](../test-p02-gates.ps1) | توثيق وحوكمة وأدلة |
| [planning/test-p03-gates.ps1](../test-p03-gates.ps1) | توثيق وحوكمة وأدلة |
| [planning/test-phase-gates.ps1](../test-phase-gates.ps1) | توثيق وحوكمة وأدلة |
| [planning/verify-p02.ps1](../verify-p02.ps1) | توثيق وحوكمة وأدلة |
| [planning/verify-p03.ps1](../verify-p03.ps1) | توثيق وحوكمة وأدلة |
| [planning/verify-phase.ps1](../verify-phase.ps1) | توثيق وحوكمة وأدلة |
| [planning/verify-roadmap.ps1](../verify-roadmap.ps1) | توثيق وحوكمة وأدلة |
