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

## Known empty districts

These14 source districts have no shiyakhas in the accepted version. Empty means no records in this file, not that no real areas exist. No invented placeholder is imported.

| Source ID | Governorate | Original Arabic name | Original English name |
| --- | --- | --- | --- |
| EG0100 | القاهرة | خارج الزمام | Zemam Out |
| EG0200 | الاسكندرية | خارج الزمام | Zemam Out |
| EG1300 | الشرقية | خارج الزمام | Zemam Out |
| EG1400 | القليوبية | خارج الزمام | Zemam Out |
| EG1800 | البحيرة | خارج الزمام | Zemam Out |
| EG2100 | الجيزة | خارج الزمام | Zemam Out |
| EG2200 | بنى سويف | خارج الزمام | Zemam Out |
| EG2300 | الفيوم | خارج الزمام | Zemam Out |
| EG2400 | المنيا | خارج الزمام | Zemam Out |
| EG2500 | أسيوط | صحراء أسيوط | Zemam Out |
| EG2600 | سوهاج | صحراء سوهاج | Zemam Out |
| EG2700 | قنا | صحراء قنا | Zemam Out |
| EG2800 | أسوان | خارج الزمام | Zemam Out |
| EG2900 | مدينة الأقصر | خارج الزمام | Zemam Out |

## Repeated English district labels

These18 groups retain distinct source identities. Names are preserved, not merged or renamed. This is an inventory of this pinned source, not a national completeness assessment.

| Governorate | English label | Distinct original Arabic names and source IDs |
| --- | --- | --- |
| الدقهلية | Mit Ghamr | قسم ميت غمر (EG1212); مركز ميت غمر (EG1213) |
| الشرقية | Faqus | قسم فاقوس (EG1311); مركز فاقوس (EG1312) |
| القليوبية | Banha | قسم بنها (EG1401); مركز بنها (EG1402) |
| القليوبية | Qalyub | قسم قليوب (EG1409); مركز قليوب (EG1410) |
| كفر الشيخ | Kafr Al-Shaykh | قسم كفر الشيخ (EG1501); مركز كفر الشيخ (EG1502) |
| كفر الشيخ | Disuq | قسم دسوق (EG1505); مركز دسوق (EG1506) |
| المنوفية | Shibin al-Kum | قسم شبين الكوم (EG1701); مركز شبين الكوم (EG1702) |
| البحيرة | Damanhur | قسم دمنهور (EG1801); مركز دمنهور (EG1802) |
| البحيرة | Kafr Al-Dawwar | قسم كفرالدوار (EG1811); مركز كفر الدوار (EG1812) |
| الجيزة | Imbaba | قسم امبابه (EG2101); مركز إمبابة (EG2113) |
| الجيزة | Giza | قسم الجيزة (EG2104); مركز الجيزة (EG2109) |
| بنى سويف | Bani Swayf | قسم بنى سويف (EG2201); مركز بنى سويف (EG2202) |
| الفيوم | Fayyum | قسم الفيوم (EG2301); مركز الفيوم (EG2302) |
| سوهاج | Suhag | قسم اول سوهاج (EG2601); مركز سوهاج (EG2603) |
| سوهاج | Girga | قسم جرجا (EG2609); مركز جرجا (EG2610) |
| قنا | Qina | قسم قنا (EG2701); مركز قنا (EG2702) |
| أسوان | Aswan | قسم أسوان (EG2801); مركز أسوان (EG2802) |
| مدينة الأقصر | Luxor | قسم الأقصر (EG2901); مركز الأقصر (EG2902) |
