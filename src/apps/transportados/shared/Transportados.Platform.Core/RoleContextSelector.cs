namespace Transportados.Platform.Core
{
    public static class RoleContextSelector
    {
        public static RoleContextSelection Select(
            IReadOnlyCollection<PlatformTenantMemberClaim> memberships,
            string? requestedRole,
            Guid? requestedTenantId,
            string? defaultRole,
            string adminRole,
            string fallbackRole)
        {
            var declaredRoles = ParseDeclaredRoles(defaultRole);
            var useDefaultRole = !string.IsNullOrWhiteSpace(requestedRole) &&
                declaredRoles.Contains(requestedRole, StringComparer.OrdinalIgnoreCase) &&
                !memberships.Any(m => string.Equals(m.Role, requestedRole, StringComparison.OrdinalIgnoreCase));

            var selected = useDefaultRole
                ? null
                : (memberships.FirstOrDefault(m =>
                    (string.IsNullOrWhiteSpace(requestedRole) || string.Equals(m.Role, requestedRole, StringComparison.OrdinalIgnoreCase)) &&
                    (!requestedTenantId.HasValue || m.TenantId == requestedTenantId.Value))
                    ?? memberships.FirstOrDefault());

            var activeRole = selected?.Role
                ?? (useDefaultRole ? requestedRole : null)
                ?? (memberships.Count > 0 ? null : declaredRoles.FirstOrDefault() ?? fallbackRole);

            return new RoleContextSelection
            {
                ActiveRole = activeRole,
                ActiveTenantId = string.Equals(selected?.Role, adminRole, StringComparison.OrdinalIgnoreCase)
                    ? selected?.TenantId
                    : null,
                SelectedMembership = selected,
                UsesDefaultRole = useDefaultRole
            };
        }

        public static List<string> ParseDeclaredRoles(string? defaultRole) =>
            string.IsNullOrWhiteSpace(defaultRole)
                ? new List<string>()
                : defaultRole.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
    }
}
