namespace EcomAI.Platform.Business.Interfaces;

public interface ITokenProtector
{
    string Protect(string plaintextToken);
    string Unprotect(string protectedToken);
}
