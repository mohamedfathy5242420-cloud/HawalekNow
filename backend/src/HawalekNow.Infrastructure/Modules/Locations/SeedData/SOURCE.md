# Egypt reference seed source

User approval: D019, 2026-11-21. The source and its documented limitations were explicitly accepted for the initial seed; completeness of all current Egyptian cities/areas is not claimed.

Source: [Open Admin Data — Egypt](https://github.com/open-admin-data/egypt-administrative-divisions), commit `76b47b22872288c0d8b0c757975980e6986945e8`, dated 2026-09-08. Data copyright and attribution: Open Admin Data / jakkrapongt (2026), distributed under CC BY 4.0 as declared in the source's LICENSE. Preserve this attribution and LICENSE.txt with redistribution. The packaged JSON files are byte-for-byte copies at the pinned commit; no names or missing regions were invented or rewritten.

| File | SHA256 | Records |
| --- | --- | --- |
| all-governorate.json | EA86B0A7C7219D1DC7C79D6FAECF1C66325F78443277CC491D8D39A94CC9CA27 | 27 |
| all-district.json | F265357BDF7248FD162F1CC23B7C8A68F966CA4BC2988FED408AEF078C1C01D8 | 365 |
| all-shiyakha.json | A4458361D556ADB1617C836D804FDE399757FA849D0ED7351A5C428E21A51002 | 5716 |

The City model represents the source's district/markaz level; Area represents its shiyakha level. There are14 districts with no areas and18 groups of repeated English district names within one governorate. The source IDs, bilingual names and parent links are preserved. Distinct IDs remain distinct even when English labels coincide. The largest names in the accepted files are50 Arabic and67 English UTF-16 characters and fit the160-character storage limit. This inventory does not establish current nationwide completeness or coordinate accuracy. Coordinates in the raw source are not used to fabricate branch locations.

Runtime seeding is offline and verifies the pinned hashes, expected record counts and parent relations before writes. Repeated runs skip previously imported source keys, preserving all Admin edits, activation decisions and deletions. Import-key receipts intentionally survive deletion; they are provenance records for idempotency, not the project's later Admin audit implementation. Catalog owns the four activity definitions and its own receipts. No production account or application volume is deleted or recreated.
