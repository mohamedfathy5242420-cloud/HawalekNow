# P04 image dependency review

Reviewed: 2026-11-25

| Package | Pinned version | License | Purpose |
| --- | --- | --- | --- |
| [Magick.NET-Q8-AnyCPU](https://www.nuget.org/packages/Magick.NET-Q8-AnyCPU/15.1.0) | [15.1.0], direct in Infrastructure | Apache-2.0 | Actual PNG/JPEG decoding and normalization for optional store logos, under D006 and Q003-04 |

Reviewed the official [signed release](https://github.com/dlemstra/Magick.NET/releases/tag/15.1.0), [license](https://raw.githubusercontent.com/dlemstra/Magick.NET/15.1.0/License.txt), [initialization and allow-list policy](https://github.com/dlemstra/Magick.NET/blob/15.1.0/docs/Initialization.md), [byte-array decoding](https://github.com/dlemstra/Magick.NET/blob/15.1.0/docs/ReadingImages.md) and [resource limits](https://github.com/dlemstra/Magick.NET/blob/15.1.0/src/Magick.NET/ResourceLimits.cs). The selected decoder supports the existing net10 target. No existing SDK or package is upgraded. The resolved Core dependency, package license notices, lockfile diff and NuGet vulnerability audit will be recorded after actual restore; review alone is not a passing audit.

Only PNG/JPEG coders are required. Delegates and other coders must be denied before native decoding, with pixel/memory/list/thread limits and no disk pixel cache. The library's Time limit is process execution time, so it must not be used as an assumed per-image timeout on a long-lived server. Input length and dimensions must be bounded separately; decode and re-encode strips metadata instead of accepting a filename extension or client MIME as proof. SVG, external URLs, PDFs, animated images and product image pipelines are outside this logo foundation.

## Actual restore and attribution evidence

Restore resolved Magick.NET-Q8-AnyCPU15.1.0 and its sole managed dependency Magick.NET.Core15.1.0, both Apache-2.0 in their official NuGet nuspec metadata at source commit80d8318173db37bd2ee9f3b53c4c736a1267e689. Comparing every prior lockfile resolved version found no existing package upgrade. The NuGet vulnerability command on2026-11-25 used api.nuget.org/v3/index.json and reported no vulnerable packages in all seven projects; this is a point-in-time advisory result.

The wrapper license is not a substitute for native component notices. The exact bundled Notice.txt is retained at Shared/Storage/ThirdParty/Magick.NET-15.1.0.NOTICE.txt withSHA256 D66C427812EFBF0FCCB98957E47DDE3FC47B41B991CB79E7B5F11D5A76185EFE. The official wrapper LICENSE hasSHA256 3DDF9BE5C28FE27DAD143A5DC76EEA25222AD1DD68934A047064E56ED2FA40C5. Both are explicit publish content and Docker allow-list inputs; Seed attribution files remain unchanged. The native notice preserves its component licenses and pinned source URLs verbatim.

A real PNG probe showed that ListLength1 rejects a one-image pixel cache in this native version; ListLength2 permits the single-image decode/strip/encode pipeline. The decoder still explicitly requires exactly one image and rejects APNG chunks, with other coder/delegate restrictions unchanged. Initial failure19tests:14passed/5failed; after the fix19passed/0failed/0skipped. Later integration/permission/JPEG tests are recorded separately when executed.

The first staged diff check rejected the upstream notice's original whitespace. The two explicit hash-pinned vendor documents use -text and -whitespace attributes to preserve their exact bytes; application code and Seed whitespace rules remain unchanged. Text diffs remain reviewable. This is the documented per-path mechanism in the [official Git attributes reference](https://git-scm.com/docs/gitattributes#_checking_whitespace_errors); the commit guard additionally requires each index blob to equal the unfiltered local upstream bytes.
