# فهرس تغييرات P03

Updated: 2026-11-21
Status: InProgress

المقارنة من main المدمجة 5e4250ff31f84c8806649c7ef1786574da7dd00a وتشمل الملفات الموجودة فعليًا في العمل الجاري. وجود ملف لا يعني نجاح التحقق أو اعتماد المرحلة؛ راجع الخطوات وverification.json. لا تعديلات Word أو جدول المستخدم أو كود P04.

| الملف | المسؤولية |
| --- | --- |
| [.gitattributes](../../.gitattributes) | توثيق وحوكمة وأدلة |
| [.github/workflows/p01-baseline.yml](../../.github/workflows/p01-baseline.yml) | توثيق وحوكمة وأدلة |
| [backend/API_EXPLORATION.md](../../backend/API_EXPLORATION.md) | توثيق وحوكمة وأدلة |
| [backend/CI.md](../../backend/CI.md) | توثيق وحوكمة وأدلة |
| [backend/MODULES.md](../../backend/MODULES.md) | توثيق وحوكمة وأدلة |
| [backend/P03_DEPENDENCIES.md](../../backend/P03_DEPENDENCIES.md) | توثيق وحوكمة وأدلة |
| [backend/PACKAGES.md](../../backend/PACKAGES.md) | توثيق وحوكمة وأدلة |
| [backend/postman/HawalekNow.P03.Local.postman_environment.json](../../backend/postman/HawalekNow.P03.Local.postman_environment.json) | Collection وEnvironment بلا أسرار |
| [backend/postman/HawalekNow.P03.postman_collection.json](../../backend/postman/HawalekNow.P03.postman_collection.json) | Collection وEnvironment بلا أسرار |
| [backend/REFERENCE_CACHE.md](../../backend/REFERENCE_CACHE.md) | توثيق وحوكمة وأدلة |
| [backend/REFERENCE_DATA_OPERATIONS.md](../../backend/REFERENCE_DATA_OPERATIONS.md) | توثيق وحوكمة وأدلة |
| [backend/src/HawalekNow.Api/Modules/Catalog/CatalogControllers.cs](../../backend/src/HawalekNow.Api/Modules/Catalog/CatalogControllers.cs) | HTTP والأمن والتوثيق |
| [backend/src/HawalekNow.Api/Modules/Locations/LocationControllers.cs](../../backend/src/HawalekNow.Api/Modules/Locations/LocationControllers.cs) | HTTP والأمن والتوثيق |
| [backend/src/HawalekNow.Api/Modules/Search/SynonymController.cs](../../backend/src/HawalekNow.Api/Modules/Search/SynonymController.cs) | HTTP والأمن والتوثيق |
| [backend/src/HawalekNow.Api/packages.lock.json](../../backend/src/HawalekNow.Api/packages.lock.json) | HTTP والأمن والتوثيق |
| [backend/src/HawalekNow.Api/Program.cs](../../backend/src/HawalekNow.Api/Program.cs) | HTTP والأمن والتوثيق |
| [backend/src/HawalekNow.Api/Shared/OpenApi/ApiDocumentTransformer.cs](../../backend/src/HawalekNow.Api/Shared/OpenApi/ApiDocumentTransformer.cs) | HTTP والأمن والتوثيق |
| [backend/src/HawalekNow.Api/Shared/Security/ReferenceDataRateLimits.cs](../../backend/src/HawalekNow.Api/Shared/Security/ReferenceDataRateLimits.cs) | HTTP والأمن والتوثيق |
| [backend/src/HawalekNow.Application/Modules/Catalog/CatalogRequests.cs](../../backend/src/HawalekNow.Application/Modules/Catalog/CatalogRequests.cs) | عقود CQRS وDTO والتحقق |
| [backend/src/HawalekNow.Application/Modules/Catalog/Contracts/IInitialActivities.cs](../../backend/src/HawalekNow.Application/Modules/Catalog/Contracts/IInitialActivities.cs) | عقود CQRS وDTO والتحقق |
| [backend/src/HawalekNow.Application/Modules/Locations/LocationRequests.cs](../../backend/src/HawalekNow.Application/Modules/Locations/LocationRequests.cs) | عقود CQRS وDTO والتحقق |
| [backend/src/HawalekNow.Application/Modules/Locations/ReferenceSeed.cs](../../backend/src/HawalekNow.Application/Modules/Locations/ReferenceSeed.cs) | عقود CQRS وDTO والتحقق |
| [backend/src/HawalekNow.Application/Modules/Search/SynonymRequests.cs](../../backend/src/HawalekNow.Application/Modules/Search/SynonymRequests.cs) | عقود CQRS وDTO والتحقق |
| [backend/src/HawalekNow.Application/Shared/Caching/IReferenceOptionsCache.cs](../../backend/src/HawalekNow.Application/Shared/Caching/IReferenceOptionsCache.cs) | عقود CQRS وDTO والتحقق |
| [backend/src/HawalekNow.Domain/Modules/Catalog/Activity.cs](../../backend/src/HawalekNow.Domain/Modules/Catalog/Activity.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Domain/Modules/Catalog/ActivitySeedKey.cs](../../backend/src/HawalekNow.Domain/Modules/Catalog/ActivitySeedKey.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Domain/Modules/Catalog/CatalogNames.cs](../../backend/src/HawalekNow.Domain/Modules/Catalog/CatalogNames.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Domain/Modules/Catalog/Category.cs](../../backend/src/HawalekNow.Domain/Modules/Catalog/Category.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Domain/Modules/Catalog/CategoryPath.cs](../../backend/src/HawalekNow.Domain/Modules/Catalog/CategoryPath.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Domain/Modules/Locations/AdministrativePlaces.cs](../../backend/src/HawalekNow.Domain/Modules/Locations/AdministrativePlaces.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Domain/Modules/Locations/LocationSeedKey.cs](../../backend/src/HawalekNow.Domain/Modules/Locations/LocationSeedKey.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Domain/Modules/Search/Synonyms.cs](../../backend/src/HawalekNow.Domain/Modules/Search/Synonyms.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Domain/Shared/Geography/GeoPoint.cs](../../backend/src/HawalekNow.Domain/Shared/Geography/GeoPoint.cs) | قواعد الوحدة المستقلة عن Framework |
| [backend/src/HawalekNow.Infrastructure/HawalekNow.Infrastructure.csproj](../../backend/src/HawalekNow.Infrastructure/HawalekNow.Infrastructure.csproj) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Catalog/CachedCatalogQueries.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Catalog/CachedCatalogQueries.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogAdministration.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogAdministration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogConfiguration.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogConfiguration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogQueries.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogQueries.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogRegistration.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogRegistration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogTreeLock.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Catalog/CatalogTreeLock.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Catalog/InitialActivities.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Catalog/InitialActivities.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/CachedLocationQueries.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/CachedLocationQueries.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/EmbeddedReferenceSeedSource.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/EmbeddedReferenceSeedSource.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationAdministration.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationAdministration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationConfiguration.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationConfiguration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationQueries.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationQueries.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationRegistration.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationRegistration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationStateLock.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/LocationStateLock.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/ReferenceDataSeeder.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/ReferenceDataSeeder.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/all-district.json](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/all-district.json) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/all-governorate.json](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/all-governorate.json) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/all-shiyakha.json](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/all-shiyakha.json) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/LICENSE.txt](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/LICENSE.txt) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/SOURCE.md](../../backend/src/HawalekNow.Infrastructure/Modules/Locations/SeedData/SOURCE.md) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Modules/Search/SynonymAdministration.cs](../../backend/src/HawalekNow.Infrastructure/Modules/Search/SynonymAdministration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/packages.lock.json](../../backend/src/HawalekNow.Infrastructure/packages.lock.json) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Shared/Caching/ReferenceOptionsCache.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Caching/ReferenceOptionsCache.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Shared/Geography/GeographyPointMapping.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Geography/GeographyPointMapping.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/DesignTimeContextFactory.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/DesignTimeContextFactory.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121111950_P03CatalogFoundation.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121111950_P03CatalogFoundation.cs) | Migration إضافية أو snapshot |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121111950_P03CatalogFoundation.Designer.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121111950_P03CatalogFoundation.Designer.cs) | Migration إضافية أو snapshot |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121121520_P03LocationsHierarchy.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121121520_P03LocationsHierarchy.cs) | Migration إضافية أو snapshot |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121121520_P03LocationsHierarchy.Designer.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121121520_P03LocationsHierarchy.Designer.cs) | Migration إضافية أو snapshot |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121124845_P03ReferenceSeedKeys.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121124845_P03ReferenceSeedKeys.cs) | Migration إضافية أو snapshot |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121124845_P03ReferenceSeedKeys.Designer.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121124845_P03ReferenceSeedKeys.Designer.cs) | Migration إضافية أو snapshot |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121155717_P03SynonymGroups.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121155717_P03SynonymGroups.cs) | Migration إضافية أو snapshot |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121155717_P03SynonymGroups.Designer.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/20261121155717_P03SynonymGroups.Designer.cs) | Migration إضافية أو snapshot |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/HawalekDbContextModelSnapshot.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/Migrations/HawalekDbContextModelSnapshot.cs) | Migration إضافية أو snapshot |
| [backend/src/HawalekNow.Infrastructure/Shared/Persistence/PersistenceRegistration.cs](../../backend/src/HawalekNow.Infrastructure/Shared/Persistence/PersistenceRegistration.cs) | تنفيذ SQL والاستعلامات والمعاملات |
| [backend/tests/ArchitectureTests/packages.lock.json](../../backend/tests/ArchitectureTests/packages.lock.json) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/ApiFoundationTests.cs](../../backend/tests/IntegrationTests/ApiFoundationTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/CatalogConcurrencyTests.cs](../../backend/tests/IntegrationTests/CatalogConcurrencyTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/CatalogHttpTests.cs](../../backend/tests/IntegrationTests/CatalogHttpTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/CatalogPersistenceTests.cs](../../backend/tests/IntegrationTests/CatalogPersistenceTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/FoundationCatalogPorts.cs](../../backend/tests/IntegrationTests/FoundationCatalogPorts.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/FoundationIdentityPorts.cs](../../backend/tests/IntegrationTests/FoundationIdentityPorts.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/FoundationLocationPorts.cs](../../backend/tests/IntegrationTests/FoundationLocationPorts.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/FoundationSearchPorts.cs](../../backend/tests/IntegrationTests/FoundationSearchPorts.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/GeographyTests.cs](../../backend/tests/IntegrationTests/GeographyTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/LocationHttpTests.cs](../../backend/tests/IntegrationTests/LocationHttpTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/LocationPersistenceTests.cs](../../backend/tests/IntegrationTests/LocationPersistenceTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/packages.lock.json](../../backend/tests/IntegrationTests/packages.lock.json) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/ReferenceCacheHttpTests.cs](../../backend/tests/IntegrationTests/ReferenceCacheHttpTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/ReferenceLockTimeoutTests.cs](../../backend/tests/IntegrationTests/ReferenceLockTimeoutTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/ReferenceSeedTests.cs](../../backend/tests/IntegrationTests/ReferenceSeedTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/SynonymHttpTests.cs](../../backend/tests/IntegrationTests/SynonymHttpTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/IntegrationTests/SynonymPersistenceTests.cs](../../backend/tests/IntegrationTests/SynonymPersistenceTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/UnitTests/CatalogModelTests.cs](../../backend/tests/UnitTests/CatalogModelTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/UnitTests/CategoryPathTests.cs](../../backend/tests/UnitTests/CategoryPathTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/UnitTests/GeoPointTests.cs](../../backend/tests/UnitTests/GeoPointTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/UnitTests/HawalekNow.UnitTests.csproj](../../backend/tests/UnitTests/HawalekNow.UnitTests.csproj) | اختبارات وفواصل عزل |
| [backend/tests/UnitTests/LocationModelTests.cs](../../backend/tests/UnitTests/LocationModelTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/UnitTests/packages.lock.json](../../backend/tests/UnitTests/packages.lock.json) | اختبارات وفواصل عزل |
| [backend/tests/UnitTests/ReferenceOptionsCacheTests.cs](../../backend/tests/UnitTests/ReferenceOptionsCacheTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/UnitTests/ReferenceSeedRulesTests.cs](../../backend/tests/UnitTests/ReferenceSeedRulesTests.cs) | اختبارات وفواصل عزل |
| [backend/tests/UnitTests/SynonymTextTests.cs](../../backend/tests/UnitTests/SynonymTextTests.cs) | اختبارات وفواصل عزل |
| [docs/CURRENT_STATE.md](../../docs/CURRENT_STATE.md) | توثيق وحوكمة وأدلة |
| [docs/DECISIONS.md](../../docs/DECISIONS.md) | توثيق وحوكمة وأدلة |
| [docs/P02_CLOSED_STATE.md](../../docs/P02_CLOSED_STATE.md) | توثيق وحوكمة وأدلة |
| [docs/P03_SOURCE_REVIEW.md](../../docs/P03_SOURCE_REVIEW.md) | توثيق وحوكمة وأدلة |
| [planning/phase-02/MERGE_RECEIPT.md](../phase-02/MERGE_RECEIPT.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/DESIGN.md](DESIGN.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/handoffs/S01-to-S02.md](handoffs/S01-to-S02.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/handoffs/S02-to-S03.md](handoffs/S02-to-S03.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/handoffs/S03-to-S04.md](handoffs/S03-to-S04.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/handoffs/S04-to-S05.md](handoffs/S04-to-S05.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/handoffs/S05-to-S06.md](handoffs/S05-to-S06.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/handoffs/S06-to-S07.md](handoffs/S06-to-S07.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/handoffs/S07-to-P04.md](handoffs/S07-to-P04.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/PHASE_REVIEW.md](PHASE_REVIEW.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/steps/S01.md](steps/S01.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/steps/S02.md](steps/S02.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/steps/S03.md](steps/S03.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/steps/S04.md](steps/S04.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/steps/S05.md](steps/S05.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/steps/S06.md](steps/S06.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/steps/S07.md](steps/S07.md) | توثيق وحوكمة وأدلة |
| [planning/phase-03/verification.json](verification.json) | توثيق وحوكمة وأدلة |
| [planning/ROADMAP.md](../ROADMAP.md) | توثيق وحوكمة وأدلة |
| [planning/test-p02-gates.ps1](../test-p02-gates.ps1) | توثيق وحوكمة وأدلة |
| [planning/test-p03-gates.ps1](../test-p03-gates.ps1) | توثيق وحوكمة وأدلة |
| [planning/test-phase-gates.ps1](../test-phase-gates.ps1) | توثيق وحوكمة وأدلة |
| [planning/verify-p02.ps1](../verify-p02.ps1) | توثيق وحوكمة وأدلة |
| [planning/verify-p03.ps1](../verify-p03.ps1) | توثيق وحوكمة وأدلة |
| [planning/verify-phase.ps1](../verify-phase.ps1) | توثيق وحوكمة وأدلة |
| [planning/verify-roadmap.ps1](../verify-roadmap.ps1) | توثيق وحوكمة وأدلة |
