using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using HawalekNow.Application.Shared.Storage;

namespace HawalekNow.Infrastructure.Shared.Storage;

public static class PrivateStoragePermissions
{
    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode FileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public static void Apply(string path, bool directory)
    {
        if (OperatingSystem.IsWindows()) SetWindows(path, directory);
        else File.SetUnixFileMode(path, directory ? DirectoryMode : FileMode);
    }

    public static void Require(string path, bool directory)
    {
        RejectLinks(path);
        if (OperatingSystem.IsWindows()) RequireWindows(path, directory);
        else if (File.GetUnixFileMode(path) != (directory ? DirectoryMode : FileMode)) throw new StorageIntegrityException();
    }

    public static void RejectLinks(string path)
    {
        var full = Path.GetFullPath(path);
        for (FileSystemInfo? current = Directory.Exists(full) ? new DirectoryInfo(full) : new FileInfo(full); current is not null;)
        {
            if (current.LinkTarget is not null || current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) throw new StorageIntegrityException();
            current = current is DirectoryInfo directory ? directory.Parent : ((FileInfo)current).Directory;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void SetWindows(string path, bool directory)
    {
        var owner = WindowsIdentity.GetCurrent().User ?? throw new StorageIntegrityException();
        FileSystemSecurity acl = directory ? new DirectorySecurity() : new FileSecurity();
        acl.SetOwner(owner); acl.SetAccessRuleProtection(true, false);
        acl.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl,
            directory ? InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit : InheritanceFlags.None,
            PropagationFlags.None, AccessControlType.Allow));
        if (directory) new DirectoryInfo(path).SetAccessControl((DirectorySecurity)acl);
        else new FileInfo(path).SetAccessControl((FileSecurity)acl);
    }

    [SupportedOSPlatform("windows")]
    private static void RequireWindows(string path, bool directory)
    {
        var owner = WindowsIdentity.GetCurrent().User ?? throw new StorageIntegrityException();
        FileSystemSecurity acl = directory ? new DirectoryInfo(path).GetAccessControl() : new FileInfo(path).GetAccessControl();
        if (!owner.Equals(acl.GetOwner(typeof(SecurityIdentifier))) || !acl.AreAccessRulesProtected) throw new StorageIntegrityException();
        var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
        if (!rules.Any(rule => rule.AccessControlType == AccessControlType.Allow && owner.Equals(rule.IdentityReference) &&
            (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl) ||
            rules.Any(rule => rule.AccessControlType == AccessControlType.Allow && !owner.Equals(rule.IdentityReference))) throw new StorageIntegrityException();
    }
}
