# Merchant workflows and operations

P04 runs on `codex/p04-implement-merchants-stores-and-branches` in [Draft PR #11](https://github.com/mohamedfathy5242420-cloud/HawalekNow/pull/11). S01–S04 are technically verified locally. CI45846221851 proves493 tests and real storage restart/permission checks on the S03 head31e8e3b3f86442c5e0eb4e909aced5b0d77e65c9; S04 has142 relevant checks, a clean build and33 repeated branch/geography checks. S05–S07 are not implemented. Full final-head CI is still required. UserApproval/V4 remain Pending under D021.

## Accounts and existing security

Follow [Identity operations](IDENTITY_OPERATIONS.md) for configuration, registration, email OTP, login, session cookies, CSRF and account recovery. Email confirmation never grants merchant approval. `Pending` and `ChangesRequested` merchants may prepare data. Public publishing requires `Approved`. A rejected merchant retains Identity account management and may read its own review reason, while the existing merchant setup policy continues to deny access. Suspension continues to make existing sessions fail live authentication; reinstatement does not invent a new session.

All paths below are relative to `/api/v1`. Mutations require HTTPS, the real cookie jar and a fresh `X-CSRF-TOKEN` obtained from `identity/csrf`. Access/refresh tokens are not request-body inputs. The actor comes from the database-backed authenticated principal. The service also reads current Identity facts through its explicit contract. Administrative decision routes share a real 60 mutations/minute quota per peer; spoofed proxy headers do not define the peer.

## Merchant review API

| Method and path | Authorization | Input/result |
| --- | --- | --- |
| GET `admin/merchants?status=Pending&page=1&pageSize=25` | AdminOnly | Bounded review list with account identity, confirmation, status, last review and rowVersion |
| GET `admin/merchants/{id}/audit?page=1&pageSize=25` | AdminOnly | Audit for this merchant account, including retained records after profile deletion |
| POST `admin/merchants/{id}/approve` | AdminOnly | `{ "rowVersion": "...", "reason": null }` |
| POST `admin/merchants/{id}/request-changes` | AdminOnly | rowVersion and required reason |
| POST `admin/merchants/{id}/reject` | AdminOnly | rowVersion and required reason |
| POST `admin/merchants/{id}/suspend` | AdminOnly | rowVersion and required reason |
| POST `admin/merchants/{id}/reinstate` | AdminOnly | rowVersion and required reason |
| GET `merchants/review` | Confirmed active merchant, owner only | Own status/reason/version; also available for Rejected |
| POST `merchants/review/resubmit` | Confirmed active merchant, owner only | `{ "rowVersion": "..." }`; ChangesRequested to Pending |

Reasons are trimmed, at most1000 UTF-16 code units, and cannot contain NUL. Review mutation responses return the new review DTO and rowVersion. Page is1–100000 and pageSize1–100. Status filters accept the five named statuses. Client-supplied actor/user/profile fields cannot change the trusted actor or owner. Validation returns the established safe400 contract; stale versions and invalid state transitions return409. Unconfirmed or inactive targets cannot be approved. No commercial documents, store completeness or tax registration are prerequisites imposed by the approval service.

## Transactions and Audit

Each review/resubmission service call owns one transaction on the existing `HawalekDbContext`/unit of work. It acquires Identity's existing transaction-owned state lock for the actor, then the target Identity account where applicable, reloads the merchant row, validates rowVersion and the allowed transition, and writes the state plus one Audit row before one SaveChanges and Commit. Independent commands must not be combined with `Send` and described as atomic. The review service is an outer transaction owner; its public mutations are not nested transaction helpers.

`operations.AuditEntries` stores only Id, ActorId, Operation, TargetKind, TargetId, Reason, OccurredAt, PreviousState and CurrentState. Times are UTC. No account Entity, PasswordHash, OTP, token, request body or exception is serialized. Reasons belong to the intended review record; audit metadata is not emitted into technical Logging. The existing pipeline logging policy is unchanged.

Actor and target references have no deletion cascade or FK into Identity, so required history can survive physical profile/account cleanup. The API has no Audit update/delete operation. Broader Audit coverage, interceptors and general before/after capture remain P08-S04 work under Q003-03; this foundation covers the P04 actions as they are implemented. No new event, Job or Outbox message is needed for S02. Notifications remain in their planned phase.

## Schema and verification

`20261125114145_P04MerchantReviewsAndAudit` adds nullable review metadata to merchant accounts, an administrative status index and the Audit table/indexes. Nullable fields preserve pre-existing accounts without fabricating historical reasons, reviewers or times. Generation used a design-only connection and did not contact the application database. Actual migration, preservation, concurrency and rollback checks run on owned random SQL databases; applying migrations to a development application database is a separate explicit operator command using the configured connection.

From the repository root, with the project-owned SQL/Mailpit services available:

```powershell
dotnet restore backend/HawalekNow.slnx --locked-mode
dotnet build backend/HawalekNow.slnx --configuration Release --no-restore --disable-build-servers -m:1
./backend/test-p02.ps1 -NoBuild -Filter 'FullyQualifiedName~Merchant|FullyQualifiedName~AuthorizationTests|FullyQualifiedName~ArchitectureTests'
dotnet format backend/HawalekNow.slnx --verify-no-changes --no-restore
./planning/verify-roadmap.ps1
./planning/verify-git-workflow.ps1
git -c safe.directory=D:/HwalekNow diff --check
```

The focused command does not replace the full solution and exact-head CI checks required for final P04 delivery. See [step records](../planning/phase-04/steps/S02.md) and [actual verification evidence](../planning/phase-04/verification.json). The user's personal trial and detailed teaching session have not been performed by this guide.

## Selected Postman review journey

Import the [P04 collection](postman/HawalekNow.P04.postman_collection.json) and [empty local environment](postman/HawalekNow.P04.Local.postman_environment.json). Configure confirmed test accounts using the existing Identity setup in an isolated review database. Keep certificate validation enabled and the real cookie jar; never export filled credentials. The collection obtains fresh principal-bound CSRF before mutations. Run selected requests; the competing review decisions are alternative journeys.

1. Login as the test Merchant and read Own review to capture its profile Id and current rowVersion.
2. Switch to the test Admin. Choose Request Changes with a reason. Its response captures the new rowVersion. Inspect Audit.
3. Switch back to the Merchant, read the reason, and resubmit using the latest version. In S02 this exercises review state; store preparation arrives in S03.
4. Switch to Admin and approve. Read Audit to see the three distinct actions and trusted actors. Reusing an old rowVersion must return409.
5. For a separate rejected-account journey, reject a Pending merchant with a reason. The Merchant may read its own reason and manage Identity, but cannot self-resubmit. For suspension, Admin suspends an Approved account; its existing merchant cookie must fail live authentication until Admin reinstates it. Admin login remains available in the collection to switch away from that cookie.

These are instructions and request artifacts, not evidence of the user's personal trial. V4 and the detailed teaching session remain Pending/Deferred respectively.

## S03 store and optional logo

S03 is technically verified on31e8e3b3f86442c5e0eb4e909aced5b0d77e65c9: CI45846221851 passes493 tests and all three jobs, and local Windows/container process-restart and unrelated-UID checks passed. Its S04 handoff is InternalReady. The initial decoder/test/Windows failures remain in the [step record](../planning/phase-04/steps/S03.md) and evidence. See the [dependency review](P04_DEPENDENCIES.md) for the pinned decoder, advisory checks, native notices and unchanged existing package versions.

| Method and path, relative to `/api/v1` | Access | Contract |
| --- | --- | --- |
| POST `merchants/store` | VerifiedMerchantSetup | `{ "activityId": "...", "details": { "name": "One name", "phone": "+201012345678" } }` |
| GET `merchants/store` | VerifiedMerchantSetup | Own store, optional-logo flag and rowVersion |
| PUT `merchants/stores/{id}` | VerifiedMerchantSetup, owner | `{ "rowVersion": "...", "details": { ... } }`; cannot reassign owner or main activity |
| PUT `merchants/stores/{id}/logo` | Same owner policy | Multipart `rowVersion` and `file`; response is the new store DTO/version |
| GET `merchants/stores/{id}/logo` | Same owner policy | Raw normalized `image/png`, not a filesystem key/path |
| DELETE `merchants/stores/{id}/logo` | Same owner policy | JSON `{ "rowVersion": "..." }`; removing an already absent logo is a no-op |
| GET `stores/{id}` | Public visibility gate | Approved merchant and confirmed active Identity account; no private owner/version fields |
| GET `stores/{id}/logo` | Same public visibility gate | Raw `image/png`; no-store and nosniff |

All mutations retain HTTPS, current-principal CSRF, live authorization and the shared merchant quota. Authentication itself ignores cookies on HTTP, including manually forwarded cookies; protected requests return401 before reaching a controller. Other merchants get404 for the private store resource. Pending/ChangesRequested may prepare it; confirming email does not publish it. `hasLogo:false` tells clients to display their placeholder; a missing optional logo returns404. No static-file middleware exposes the storage directory.

The store name is one trimmed field of1–200 UTF-16 code units, with no controls. One of Phone/WhatsApp is required. Each channel accepts E.164 (`+` and7–15 digits); formatted spaces/hyphens/parentheses are removed, and Egyptian national10/11-digit numbers beginning0 become `+20...`. Optional Facebook/Instagram/TikTok links are bounded HTTPS URLs on the corresponding allowed service hosts, without userinfo or alternate ports. They are stored as links and never fetched by the server. Catalog selection must exist and be active, checked through its transaction-owned contract. Later deactivation stops new selection; it does not change an existing store's activity or silently hide the store. Activity changes arrive only through S06.

`20261126141141_P04StoresAndLogoStorage` creates the store table with a unique merchant-account FK, contact/name constraints, rowVersion and a unique optional logo key. A primitive cross-schema FK to `catalog.Activities` prevents deleting an activity in use, without referencing another module's Entity/navigation. Generation used a nonconnecting DesignOnly configuration; it has not been applied to the application database. SQL tests migrate only newly owned random databases.

## Storage boundaries and failure handling

The default root is `App_Data/StoreFiles` under the API content root. Configure `FileStorage:Root` as a stable absolute local path outside webroot, preferably outside the checkout for deployment. The runtime directory is excluded from Git and Docker in all case variants of App_Data. A populated unmarked directory, traversal key, symlink/junction, changed ownership marker, permissive ACL/mode or changed image hash is rejected. The application does not take ownership of a foreign populated directory. Windows ACLs grant only the current owner; Unix directories/files are0700/0600. Keep the same root and persistent volume across restarts; changing the configured root is not an automatic file migration.

Only one actual PNG/JPEG image is accepted, at most2MiB input and2048×2048. Server-side signature checks and decoding, restricted coders/delegates, bounded native resources, APNG rejection and PNG re-encoding remove metadata and trailing JPEG payloads. The client filename/MIME never selects a path or decoder. Normalized output is bounded at8MiB. No SVG/PDF/GIF, remote URL, document or product-image pipeline is introduced. Keys are server-generated GUIDs; private manifests bind them to this installation, dimensions, creation time, length and SHA256. File writes use create-new temporary files, flush to disk and atomic moves; a complete file exists before SQL can reference it.

Each store mutation owns one existing DbContext/UoW transaction and acquires Identity's state lock. State changes and bounded Audit entries save together. Upload also holds a transaction-owned per-key SQL lock. If Save/Commit fails, the primary transaction is disposed before compensation. Compensation starts a fresh transaction, reacquires the key lock and checks committed SQL references before deleting. This handles a lost Commit acknowledgement: a file already referenced by a committed row is retained. If SQL/storage cannot prove it is unreferenced and owned, it is retained. Storage failures use the closed `Unavailable` error kind:503 with `service.unavailable` and the existing safe ProblemDetails envelope; no path, filename, native exception or input is exposed.

Replacing/removing a logo and final account purge write encrypted `Merchants.DeleteLogo.v1` intents in the same SQL transaction as removing the reference. The existing Outbox pump invokes their consumer only after Commit. It checks references again, removes only verified owned bytes, and uses the established retry/backoff/terminal-failure mechanism; no OTP transport changes. Storage is resolved only while handling this message kind, so a file failure does not block resolving OTP handlers. Audit remains independent of operational logging and survives account/store deletion.

An hourly `merchant-logo-reconciliation-v1` job on the `storage` queue examines owned manifests older than24 hours, checks SQL under the same per-key lock and removes unreferenced verified files. It keeps linked files and changed/unowned files. The worker retains the `identity` queue and also services `storage`. This job repairs interrupted upload compensation after storage/SQL becomes available. Incomplete temporary writes without a verified manifest are conservatively retained and never served; operator cleanup must establish ownership before removing such debris. No distributed transaction or immediate cleanup guarantee is claimed.

## Storage runtime verification

After committing production sources and building an exact-head image with the canonical test, run:

```powershell
./docker/test-api-build-context.ps1 -Image hawaleknow/api:p04-review -KeepImage
./docker/smoke-api.ps1 -Image hawaleknow/api:p04-review
./docker/test-store-files.ps1 -Image hawaleknow/api:p04-review
```

The last command builds a standalone verifier against the committed Infrastructure project. It writes/reads from two separate host processes, then two separate non-root containers using a newly labeled project volume, and checks that UID65534 cannot read the known existing image or write its directory. It does not start API Program or connect to SQL. Its JSON receipt records source head, image, outcomes and the retained volume name. The volume is preserved. These commands are instructions until their actual receipts are recorded; the earlier P03 smoke alone does not prove storage persistence or permissions.

For the selected Postman store journey, use a confirmed Merchant session, select an active Catalog activity, create the store and optionally choose a local PNG/JPEG in the multipart request. Refresh the store version after each edit/upload. An Admin approves the merchant separately; public store/logo requests then become available. The collection includes only implemented merchant-phase routes. The personal trial remains Deferred and V4 Pending.

## S04 branches and pickup points

S04 is technically verified locally:142 broad checks, a clean build0/0 and33 followup Branch/Geography checks pass with0 failed/skipped; full format verification passed. Final exact-head full CI is recorded separately. A store can have multiple places of named kind `Branch` or `PickupPoint`. The single name/address fields are merchant text. Governorate/City/Area are existing bilingual administrative references. `publicFixedAddressConfirmed:true` explicitly confirms a fixed publicly accessible business/pickup address; the server does not infer whether an address is a private home or integrate Google Maps.

| Method and path, relative to `/api/v1` | Access | Contract |
| --- | --- | --- |
| POST `merchants/stores/{storeId}/branches` | VerifiedMerchantSetup and owner | BranchWrite below |
| GET `merchants/stores/{storeId}/branches?page=1&pageSize=25` | Same owner policy | Bounded private list, own and effective contact channels, rowVersion |
| PUT `merchants/stores/{storeId}/branches/{id}` | Same owner policy | `{ "rowVersion": "...", "details": { ... } }`; store ownership is immutable |
| GET `stores/{storeId}/branches?page=1&pageSize=25` | Parent public-store gate | Bounded places with effective contacts and coordinates; no rowVersion |
| GET `branches/{id}` | Parent public-store gate | Public place details |

```json
{
  "kind": "PickupPoint",
  "name": "نقطة الاستلام",
  "address": "عنوان تجاري عام ثابت",
  "governorateId": "<selected governorate id>",
  "cityId": "<selected city id>",
  "areaId": "<selected area id>",
  "latitude": 30.0444,
  "longitude": 31.2357,
  "publicFixedAddressConfirmed": true,
  "phone": null,
  "whatsApp": "+201112345678"
}
```

Names are1–200 and addresses1–600 trimmed UTF-16 code units without controls. Both coordinates must be supplied, finite and within latitude±90/longitude±180. Phone and WhatsApp are independently optional overrides; each missing channel falls back to that channel on the current store. A WhatsApp override does not suppress the fallback Phone. Store contact requirements remain in force. Owner mutations keep live Identity/setup checks, HTTPS, CSRF, quotas and optimistic concurrency. Confirmed Pending/ChangesRequested may prepare places privately. After merchant approval, newly created places become public through their parent gate without a separate branch approval.

The Locations-owned `IAdministrativeDirectory` checks a full active path under its shared transaction-owned hierarchy lock. The global migration adds a composite FK `(AreaId,CityId,GovernorateId)` to the Locations-owned unique index `(Id,CityId,GovernorateId)`. The existing Area→City/Governorate relationships complete the chain. This prevents mismatched triples and deletion/reparenting of an area in use, including direct SQL or concurrent writers. Unused areas can still be reparented: the supporting index is not an EF immutable alternate key. Existing reference deactivation stops new selections; it does not silently remove a place already attached to those references. Unchanged administrative IDs may be retained during a contact/name edit.

`GeoPoint` is stored as SQL Server `geography`, SRID4326, with X=Longitude/Y=Latitude. SQL checks require a nonempty Point with SRID4326. Domain/Application DTOs do not expose NTS types. Production and every test context use the existing SQL/NTS provider configuration. The actual SQL tests check `.Lat`, `.Long`, `.STSrid`, shape rejection, FK violations/races, rowVersion and state+Audit rollback after Save. No distance/search endpoint or map integration is claimed.

`20261126152640_P04BranchesAndAdministrativeReferences` adds the place table, geography/contact/rowVersion fields and the supporting administrative index/FK. Owned places cascade only when the existing final Identity purge removes their parent store; soft account deletion hides them immediately while retaining data and independent Audit. It has not been applied to the application database. Weekly hours and open/next calculations are still S05 work, required before final P04 delivery; S04 alone is not the complete merchant phase.
