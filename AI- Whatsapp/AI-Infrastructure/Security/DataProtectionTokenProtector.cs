using EcomAI.Platform.Business.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace EcomAI.Platform.Infrastructure.Security;

public sealed class DataProtectionTokenProtector : ITokenProtector
{
    private const string Purpose = "EcomAI.Meta.Tokens.v1";
    private readonly IDataProtector _protector;

    public DataProtectionTokenProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Protect(string plaintextToken)
    {
        return _protector.Protect(plaintextToken);
    }

    public string Unprotect(string protectedToken)
    {
        return _protector.Unprotect(protectedToken);
    }
}
