using Transportados.Platform.Core;

namespace Transportados.Platform.EntityFramework
{
    public static class TenantAccessValidator
    {
        public static void ValidateTenantAccess<T>(
            T? entity,
            IReadOnlyCollection<Guid>? allowedTenantIds,
            bool isSuperAdmin,
            Func<string, Exception> notFoundFactory,
            Func<string, Exception> unauthorizedFactory)
            where T : class, ITransportadosTenantEntity
        {
            if (isSuperAdmin)
            {
                return;
            }

            if (entity == null)
            {
                throw notFoundFactory($"{typeof(T).Name} not found");
            }

            if (allowedTenantIds == null || !allowedTenantIds.Contains(entity.TenantId))
            {
                throw unauthorizedFactory($"Access denied: {typeof(T).Name} belongs to a different tenant");
            }
        }

        public static void RequireValidTenantContext(
            IReadOnlyCollection<Guid>? allowedTenantIds,
            bool isSuperAdmin,
            Func<string, Exception> unauthorizedFactory)
        {
            if (isSuperAdmin)
            {
                return;
            }

            if (allowedTenantIds == null || allowedTenantIds.Count == 0)
            {
                throw unauthorizedFactory(
                    "Access denied: No valid tenant context. Authentication may have failed or user has no tenant memberships.");
            }
        }
    }
}
