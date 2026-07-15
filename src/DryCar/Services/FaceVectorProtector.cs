using Microsoft.AspNetCore.DataProtection;

namespace DryCar.Services;

/// <summary>
/// Yüz fotoğrafını değil, fotoğraftan üretilen biyometrik şablonu korur.
/// Anahtar halkası üretimde uygulama klasörünün dışında ve yedekli tutulmalıdır.
/// </summary>
public sealed class FaceVectorProtector : IFaceVectorProtector
{
    private readonly IDataProtector _protector;

    public FaceVectorProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("DryCar.FaceVector.v1");
    }

    public string Protect(string faceVector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(faceVector);
        return _protector.Protect(faceVector);
    }

    public string Unprotect(string protectedFaceVector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedFaceVector);
        return _protector.Unprotect(protectedFaceVector);
    }
}
