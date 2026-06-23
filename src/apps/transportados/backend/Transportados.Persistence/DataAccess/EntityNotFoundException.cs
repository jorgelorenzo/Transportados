namespace Transportados.Persistence.DataAccess;

public sealed class EntityNotFoundException(string message) : Exception(message);
