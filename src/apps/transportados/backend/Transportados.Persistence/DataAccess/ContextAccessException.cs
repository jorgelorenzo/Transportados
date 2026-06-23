namespace Transportados.Persistence.DataAccess;

public sealed class ContextAccessException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
