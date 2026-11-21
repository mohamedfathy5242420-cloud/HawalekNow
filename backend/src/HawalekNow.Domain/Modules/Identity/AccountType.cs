namespace HawalekNow.Domain.Modules.Identity;

// Account type is fixed at creation. Administrative permissions are never accepted from registration input.
public enum AccountType { Customer = 1, Merchant = 2, Admin = 3 }
