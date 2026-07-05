using System.IO.Pipes;

namespace Saku_Overclock.Service;

public interface IIpcSecurityService
{
    bool ValidateClientSignature(NamedPipeServerStream pipe);
}