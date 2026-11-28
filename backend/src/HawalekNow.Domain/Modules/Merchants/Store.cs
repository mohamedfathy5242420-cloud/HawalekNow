namespace HawalekNow.Domain.Modules.Merchants;

public enum StoreModerationAction { Hide = 1, Publish = 2, Suspend = 3, Resume = 4 }

public sealed class Store
{
    public Guid Id { get; private set; }
    public Guid MerchantAccountId { get; private set; }
    public Guid ActivityId { get; private set; }
    public string Name { get; private set; } = "";
    public string? Phone { get; private set; }
    public string? WhatsApp { get; private set; }
    public string? Facebook { get; private set; }
    public string? Instagram { get; private set; }
    public string? TikTok { get; private set; }
    public string? LogoKey { get; private set; }
    public bool IsHidden { get; private set; }
    public bool IsSuspended { get; private set; }
    public string? VisibilityReason { get; private set; }
    public string? SuspensionReason { get; private set; }
    public bool CanOwnerEdit => !IsSuspended;
    public bool CanAppearPublicly => !IsHidden && !IsSuspended;
    public byte[] RowVersion { get; private set; } = [];
    private Store() { }

    public static Store Create(Guid merchantAccountId, Guid activityId, StoreDetails details)
    {
        if (merchantAccountId == Guid.Empty || activityId == Guid.Empty) throw new ArgumentException("An owner and activity are required.");
        var store = new Store { Id = Guid.CreateVersion7(), MerchantAccountId = merchantAccountId, ActivityId = activityId };
        store.Update(details); return store;
    }
    public void Update(StoreDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        Name = details.Name; Phone = details.Phone; WhatsApp = details.WhatsApp;
        Facebook = details.Facebook; Instagram = details.Instagram; TikTok = details.TikTok;
    }
    public void SetLogo(string? key)
    {
        if (key is not null && (key.Length != 32 || key.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))) throw new ArgumentException("A generated image key is required.");
        LogoKey = key;
    }
    public bool TryApplyApprovedActivity(Guid previousActivityId, Guid requestedActivityId)
    {
        if (ActivityId != previousActivityId || requestedActivityId == Guid.Empty || requestedActivityId == ActivityId) return false;
        ActivityId = requestedActivityId; return true;
    }
    public bool TryModerate(StoreModerationAction action, string? reason)
    {
        if (!AdminReason(reason)) return false;
        switch (action)
        {
            case StoreModerationAction.Hide when !IsHidden: IsHidden = true; VisibilityReason = reason!.Trim(); return true;
            case StoreModerationAction.Publish when IsHidden: IsHidden = false; VisibilityReason = reason!.Trim(); return true;
            case StoreModerationAction.Suspend when !IsSuspended: IsSuspended = true; SuspensionReason = reason!.Trim(); return true;
            case StoreModerationAction.Resume when IsSuspended: IsSuspended = false; SuspensionReason = reason!.Trim(); return true;
            default: return false;
        }
    }
    public static bool AdminReason(string? reason) => !string.IsNullOrWhiteSpace(reason) && StoreActivityChange.ValidReason(reason);
}

public sealed record StoreDetails
{
    public string Name { get; }
    public string? Phone { get; }
    public string? WhatsApp { get; }
    public string? Facebook { get; }
    public string? Instagram { get; }
    public string? TikTok { get; }
    private StoreDetails(string name, string? phone, string? whatsApp, string? facebook, string? instagram, string? tikTok) =>
        (Name, Phone, WhatsApp, Facebook, Instagram, TikTok) = (name, phone, whatsApp, facebook, instagram, tikTok);

    public static bool TryCreate(string? name, string? phone, string? whatsApp, string? facebook, string? instagram, string? tikTok, out StoreDetails? details)
    {
        details = null; name = name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200 || name.Any(char.IsControl) ||
            !Contact(phone, out var normalizedPhone) || !Contact(whatsApp, out var normalizedWhatsApp) || normalizedPhone is null && normalizedWhatsApp is null ||
            !Social(facebook, ["facebook.com", "www.facebook.com", "m.facebook.com", "fb.com", "www.fb.com"], out var normalizedFacebook) ||
            !Social(instagram, ["instagram.com", "www.instagram.com"], out var normalizedInstagram) ||
            !Social(tikTok, ["tiktok.com", "www.tiktok.com", "vm.tiktok.com", "vt.tiktok.com"], out var normalizedTikTok)) return false;
        details = new(name, normalizedPhone, normalizedWhatsApp, normalizedFacebook, normalizedInstagram, normalizedTikTok); return true;
    }

    public static bool Contact(string? value, out string? normalized)
    {
        normalized = null; if (string.IsNullOrWhiteSpace(value)) return true;
        if (value.Length > 40 || value.Any(character => char.IsControl(character) || !(char.IsAsciiDigit(character) || character is '+' or ' ' or '-' or '(' or ')'))) return false;
        value = new string(value.Where(character => character is not (' ' or '-' or '(' or ')')).ToArray());
        if (value.StartsWith('0') && value.Length is 10 or 11 && value.All(char.IsAsciiDigit)) value = "+20" + value[1..];
        if (value.Length is < 8 or > 16 || value[0] != '+' || value[1] is < '1' or > '9' || !value[1..].All(char.IsAsciiDigit)) return false;
        normalized = value; return true;
    }
    private static bool Social(string? value, string[] hosts, out string? normalized)
    {
        normalized = null; if (string.IsNullOrWhiteSpace(value)) return true;
        value = value.Trim();
        if (value.Length > 500 || value.Any(char.IsControl) || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0 || !hosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase)) return false;
        normalized = uri.AbsoluteUri; return normalized.Length <= 500;
    }
}
