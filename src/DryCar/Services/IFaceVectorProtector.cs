namespace DryCar.Services;

public interface IFaceVectorProtector
{
    string Protect(string faceVector);

    string Unprotect(string protectedFaceVector);
}
