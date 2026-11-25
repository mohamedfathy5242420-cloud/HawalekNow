# Merchant workflows and operations

P04 runs on `codex/p04-implement-merchants-stores-and-branches` in [Draft PR #11](https://github.com/mohamedfathy5242420-cloud/HawalekNow/pull/11). S01 is technically verified. S02 passed its focused build and139 unit/SQL/HTTP/authorization/architecture checks; final format/documentation checks and exact-head CI are recorded separately when complete. The phase remains under execution authorization D021, with UserApproval/V4 Pending.

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
