using System.Globalization;
using System.Net.Mail;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Transportados.Contracts.Api.Dto;
using Transportados.Domain.Api.Domain;
using Transportados.Platform.Core;

namespace Transportados.Persistence.DataAccess;

public interface ITransportadosDataService
{
    AuthenticatedContextDto? LoggedUser { get; set; }
    Task<AuthenticatedContextDto?> Login(LoginRequestDto request);
    Task GenerateVerificationCode(string email, string? publicOrigin = null);
    Task ChangePasswordWithCode(string email, string verificationCode, string newPassword);
    Task<AuthenticatedContextDto?> BuildUserContext(Guid userId, string? activeRole, Guid? activeTenantId, string? appContext);
    Task<RegisterCompanyAccountResponseDto> RegisterCompanyAccount(RegisterCompanyAccountRequestDto request, string? publicOrigin = null);
    Task<List<DemoUserDto>> GetDemoUsers();
    Task<AuthenticatedContextDto> DemoLogin(Guid userId);
    Task<TenantStatus?> GetTenantStatus(Guid id);
    Task<List<TenantMember>> GetTenantMembersForUser(Guid userId);
    Task<List<TenantMember>> GetTenantMembersForTenant(Guid tenantId);
    Task<List<Guid>> GetAllowedTenantIdsForUser(Guid userId);
    Task<CustomerListResponseDto> GetCustomers(CustomerQueryDto query);
    Task<List<CustomerDto>> GetCustomerList(string? search, int take = 200);
    Task<CustomerDto?> GetCustomer(Guid id);
    Task<CustomerDto> SaveCustomer(CustomerDto request);
    Task DeleteCustomer(Guid id);
    Task<UserListResponseDto> GetUsers(UserQueryDto query);
    Task<List<UserDto>> GetUserList();
    Task<UserDto?> GetUser(Guid id);
    Task<UserDto?> GetUserByEmail(string email);
    Task<UserDto> SaveUser(UserSaveRequestDto request);
    Task DeleteUser(Guid id);
    Task<SettingsDto> GetSettings();
    Task<SettingsDto> SaveSettings(SettingsDto request);
    Task<TenantFeatureFlagsDto> GetActiveTenantFeatureFlags();
    Task<List<TenantListItemDto>> GetTenantList(int page, int pageSize);
    Task<TenantDetailDto?> GetTenantById(Guid id);
    Task<bool> UpdateTenant(Guid id, TenantUpdateRequestDto request, string? publicOrigin = null);
    Task<bool> DeleteTenant(Guid id);
    Task<bool> PhysicallyDeleteTenant(Guid id, TenantPhysicalDeleteRequestDto request);
    Task<PlatformStatsDto> GetPlatformStats();
}

public sealed class ContextSelectionRequiredException(string message) : Exception(message);

public sealed class EFDataServices(
    TransportadosDbContext dbContext,
    PasswordHashingManager passwordHashingManager,
    IEmailSender? emailSender = null,
    EmailTransportOptions? emailTransportOptions = null,
    ILogger<EFDataServices>? logger = null) : ITransportadosDataService
{
    private const string DefaultAdminPassword = "admin";
    private const string PendingCompanyNotificationTemplateKey = "pending-company-notification";
    private const string CompanyAdminFirstLoginTemplateKey = "company-admin-first-login";

    private readonly IEmailSender? _emailSender = emailSender;
    private readonly EmailTransportOptions _emailTransportOptions = emailTransportOptions ?? new EmailTransportOptions();
    private readonly ILogger<EFDataServices> _logger = logger ?? NullLogger<EFDataServices>.Instance;

    public AuthenticatedContextDto? LoggedUser { get; set; }

    public async Task<AuthenticatedContextDto?> Login(LoginRequestDto request)
    {
        var normalizedEmail = NormalizeEmail(request.Username);
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.Deleted);

        if (user == null)
        {
            return null;
        }

        var password = await dbContext.UserPasswords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == user.Id && !p.Deleted);

        if (password == null || !VerifyPassword(request.Password, password.HashedPassword))
        {
            return null;
        }

        return await BuildUserContext(user.Id, request.ActiveRole, request.ActiveTenantId, request.AppContext);
    }

    public async Task GenerateVerificationCode(string email, string? publicOrigin = null)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Debe indicar un email.");
        }

        var normalizedEmail = NormalizeEmail(email);
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.Deleted);

        if (user == null)
        {
            _logger.LogInformation("Password recovery code requested for an unknown email.");
            return;
        }

        if (_emailSender?.IsEnabled != true)
        {
            throw new InvalidOperationException("Envio de email no configurado para recuperar claves.");
        }

        var tenantId = (await GetTenantMembersForUser(user.Id)).FirstOrDefault()?.TenantId;
        var tenantSettings = tenantId.HasValue ? await GetSettingsEntity(tenantId.Value) : NewEmptySettings();
        var transport = _emailTransportOptions.ToTransportSettings(tenantSettings);
        if (!EmailTransportOptions.IsTransportConfigured(transport))
        {
            throw new InvalidOperationException("Transporte de email no configurado. Complete la configuracion SMTP del tenant.");
        }

        var existingCodes = await dbContext.UserVerificationCodes
            .Where(code => code.Email == normalizedEmail)
            .ToListAsync();
        dbContext.UserVerificationCodes.RemoveRange(existingCodes);

        var verificationCode = new UserVerificationCode
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            VerificationCode = GenerateSixDigitString(),
            CreationDate = DateTime.UtcNow
        };

        dbContext.UserVerificationCodes.Add(verificationCode);
        await dbContext.SaveChangesAsync();

        var directAccessLink = BuildPasswordRecoveryLink(publicOrigin, normalizedEmail, verificationCode.VerificationCode);
        var response = await _emailSender.SendEmailAsync(new EmailSendRequest
        {
            TenantId = tenantId ?? Guid.Empty,
            TemplateKey = "password-recovery",
            IdempotencyKey = CreateIdempotencyKey("transportados-password-recovery", normalizedEmail, verificationCode.VerificationCode),
            RecipientEmail = normalizedEmail,
            RecipientDisplayName = user.FullName,
            Subject = $"Codigo de recuperacion de clave solicitado para {normalizedEmail}",
            Body = BuildPasswordRecoveryEmailBody(normalizedEmail, verificationCode.VerificationCode, directAccessLink),
            Transport = transport
        });

        if (!response.Success)
        {
            throw new InvalidOperationException(response.ErrorMessage ?? "No se pudo enviar el email de recuperacion de clave.");
        }
    }

    public async Task ChangePasswordWithCode(string email, string verificationCode, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Debe indicar un email.");
        }

        if (string.IsNullOrWhiteSpace(verificationCode))
        {
            throw new InvalidOperationException("Debe indicar el codigo de verificacion.");
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            throw new InvalidOperationException("Debe indicar la nueva clave.");
        }

        var normalizedEmail = NormalizeEmail(email);
        var normalizedCode = verificationCode.Trim();
        var verification = await dbContext.UserVerificationCodes
            .FirstOrDefaultAsync(code => code.Email == normalizedEmail && code.VerificationCode == normalizedCode);

        if (verification == null)
        {
            throw new InvalidOperationException("Codigo de verificacion invalido.");
        }

        if (verification.CreationDate < DateTime.UtcNow.AddMinutes(-15))
        {
            throw new InvalidOperationException("El codigo vencio, tiene 15 minutos para utilizarlo.");
        }

        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.Deleted)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        await UpsertPassword(user.Id, newPassword);

        var existingCodes = await dbContext.UserVerificationCodes
            .Where(code => code.Email == normalizedEmail)
            .ToListAsync();
        dbContext.UserVerificationCodes.RemoveRange(existingCodes);
        await dbContext.SaveChangesAsync();
    }

    public async Task<AuthenticatedContextDto?> BuildUserContext(Guid userId, string? activeRole, Guid? activeTenantId, string? appContext)
    {
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.Deleted);

        if (user == null)
        {
            return null;
        }

        var memberships = await GetTenantMembersForUser(user.Id);
        var membershipDtos = memberships.Select(TransportadosMaps.ToTenantMemberInfoDto).ToList();
        var activeTenantIds = await dbContext.Tenants
            .IgnoreQueryFilters()
            .Where(t => !t.Deleted && t.Status == TenantStatus.Active)
            .Select(t => t.Id)
            .ToListAsync();

        var requestedRole = NormalizeRole(activeRole);
        var requestedTenantId = NormalizeTenantId(activeTenantId);
        string? selectedRole;
        Guid? selectedTenantId;

        if (user.IsSuperAdmin)
        {
            selectedTenantId = requestedTenantId ?? NormalizeTenantId(user.LastActiveTenantId);
            if (selectedTenantId.HasValue && !activeTenantIds.Contains(selectedTenantId.Value))
            {
                if (requestedTenantId.HasValue)
                {
                    throw new InvalidOperationException("Seleccion de tenant invalida.");
                }

                selectedTenantId = null;
            }

            selectedRole = requestedRole ?? Roles.SuperAdmin;
        }
        else
        {
            var candidates = memberships.Where(m => activeTenantIds.Contains(m.TenantId)).ToList();
            if (candidates.Count == 0)
            {
                throw new InvalidOperationException("El usuario no tiene tenants activos disponibles.");
            }

            TenantMember? selectedMembership = null;
            if (requestedTenantId.HasValue)
            {
                selectedMembership = candidates.FirstOrDefault(m => m.TenantId == requestedTenantId.Value);
                if (selectedMembership == null)
                {
                    throw new InvalidOperationException("Seleccion de tenant invalida.");
                }
            }

            selectedMembership ??= candidates.FirstOrDefault(m => m.TenantId == user.LastActiveTenantId);
            selectedMembership ??= candidates.First();

            var normalizedMembershipRole = NormalizeRole(selectedMembership.Role);
            if (!string.IsNullOrWhiteSpace(requestedRole) &&
                !string.Equals(requestedRole, normalizedMembershipRole, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Seleccion de rol invalida.");
            }

            selectedTenantId = selectedMembership.TenantId;
            selectedRole = normalizedMembershipRole;
        }

        await PersistLastContextAsync(user, selectedRole, selectedTenantId);

        return new AuthenticatedContextDto
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            IsSuperAdmin = user.IsSuperAdmin,
            IsDemo = user.IsDemo,
            TenantMemberships = membershipDtos,
            AllowedTenantIds = user.IsSuperAdmin ? activeTenantIds : membershipDtos.Select(m => m.TenantId).Distinct().ToList(),
            ActiveRole = selectedRole,
            ActiveTenantId = selectedTenantId,
            AppContext = appContext,
            DefaultRole = user.DefaultRole,
            ActiveTenantFeatures = selectedTenantId.HasValue ? await BuildFeatureFlags(selectedTenantId.Value) : new TenantFeatureFlagsDto(),
            TechRoleLabel = selectedTenantId.HasValue ? await ResolveTechRoleLabel(selectedTenantId.Value) : Roles.DefaultTechRoleLabel
        };
    }

    public async Task<RegisterCompanyAccountResponseDto> RegisterCompanyAccount(RegisterCompanyAccountRequestDto request, string? publicOrigin = null)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName))
        {
            throw new InvalidOperationException("Debe indicar el nombre de la empresa.");
        }

        if (string.IsNullOrWhiteSpace(request.ContactFullName))
        {
            throw new InvalidOperationException("Debe indicar el nombre del contacto.");
        }

        if (string.IsNullOrWhiteSpace(request.ContactEmail) || !IsValidEmail(request.ContactEmail))
        {
            throw new InvalidOperationException("Debe indicar un email de contacto valido.");
        }

        var normalizedEmail = NormalizeEmail(request.ContactEmail);
        if (await dbContext.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == normalizedEmail && !u.Deleted))
        {
            throw new InvalidOperationException("Ya existe un usuario con ese email.");
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.CompanyName.Trim(),
            Slug = await CreateUniqueSlug(request.CompanyName),
            Status = TenantStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            RegistrationContactFullName = request.ContactFullName.Trim(),
            RegistrationContactEmail = normalizedEmail,
            RegistrationContactPhone = NullIfEmpty(request.ContactPhone),
            RegistrationAddressLine = NullIfEmpty(request.AddressLine),
            RegistrationCity = NullIfEmpty(request.City),
            FeatureAppearanceEnabled = true,
            FeatureEmailEnabled = true
        };
        dbContext.Tenants.Add(tenant);

        var admin = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.ContactFullName.Trim(),
            Email = normalizedEmail,
            CreatedAt = DateTime.UtcNow,
            DefaultRole = Roles.Admin,
            LastActiveTenantId = tenant.Id
        };
        dbContext.Users.Add(admin);
        await UpsertPassword(admin.Id, DefaultAdminPassword);

        dbContext.TenantMembers.Add(new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = admin.Id,
            Role = Roles.Admin
        });

        dbContext.Settings.Add(new Settings
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = tenant.Name,
            TechRoleLabel = Roles.DefaultTechRoleLabel
        });

        await dbContext.SaveChangesAsync();
        await NotifyCompanyRegistrationAsync(tenant, admin, publicOrigin);

        return new RegisterCompanyAccountResponseDto
        {
            Tenant = TransportadosMaps.ToTenantInfoDto(tenant),
            RequiresApproval = true,
            Message = "La solicitud fue registrada y queda pendiente de aprobacion."
        };
    }

    public async Task<List<DemoUserDto>> GetDemoUsers()
    {
        var demoUsers = await dbContext.DemoUsers
            .IgnoreQueryFilters()
            .Include(d => d.User)
            .Where(d => !d.Deleted && d.User != null && !d.User.Deleted)
            .OrderBy(d => d.DisplayOrder)
            .ToListAsync();

        var result = new List<DemoUserDto>();
        foreach (var demo in demoUsers)
        {
            var membership = demo.User == null
                ? null
                : (await GetTenantMembersForUser(demo.User.Id)).FirstOrDefault();
            result.Add(new DemoUserDto
            {
                UserId = demo.UserId,
                FullName = demo.User?.FullName ?? string.Empty,
                Email = demo.User?.Email ?? string.Empty,
                Role = membership?.Role ?? (demo.User?.IsSuperAdmin == true ? Roles.SuperAdmin : string.Empty),
                TenantName = membership?.Tenant?.Name ?? string.Empty,
                DisplayOrder = demo.DisplayOrder
            });
        }

        return result;
    }

    public async Task<AuthenticatedContextDto> DemoLogin(Guid userId)
    {
        var demoUser = await dbContext.DemoUsers
            .IgnoreQueryFilters()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.UserId == userId && !d.Deleted && d.User != null && !d.User.Deleted)
            ?? throw new InvalidOperationException("Usuario demo no encontrado.");

        var context = await BuildUserContext(demoUser.UserId, null, null, null);
        return context ?? throw new InvalidOperationException("No se pudo iniciar sesion demo.");
    }

    public async Task<TenantStatus?> GetTenantStatus(Guid id) =>
        await dbContext.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == id && !t.Deleted)
            .Select(t => (TenantStatus?)t.Status)
            .FirstOrDefaultAsync();

    public async Task<List<TenantMember>> GetTenantMembersForUser(Guid userId) =>
        await dbContext.TenantMembers
            .IgnoreQueryFilters()
            .Include(m => m.User)
            .Include(m => m.Tenant)
            .Where(m => m.UserId == userId && !m.Deleted && m.Tenant != null && !m.Tenant.Deleted)
            .OrderBy(m => m.Tenant!.Name)
            .ThenBy(m => m.Role)
            .ToListAsync();

    public async Task<List<TenantMember>> GetTenantMembersForTenant(Guid tenantId)
    {
        EnsureCanAccessTenant(tenantId);
        return await dbContext.TenantMembers
            .IgnoreQueryFilters()
            .Include(m => m.User)
            .Include(m => m.Tenant)
            .Where(m => m.TenantId == tenantId && !m.Deleted && m.User != null && !m.User.Deleted)
            .OrderBy(m => m.User!.FullName)
            .ToListAsync();
    }

    public async Task<List<Guid>> GetAllowedTenantIdsForUser(Guid userId)
    {
        var user = await dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId && !u.Deleted);
        if (user == null)
        {
            return [];
        }

        if (user.IsSuperAdmin)
        {
            return await dbContext.Tenants
                .IgnoreQueryFilters()
                .Where(t => !t.Deleted && t.Status == TenantStatus.Active)
                .Select(t => t.Id)
                .ToListAsync();
        }

        return await dbContext.TenantMembers
            .IgnoreQueryFilters()
            .Where(m => m.UserId == userId && !m.Deleted && m.Tenant != null && !m.Tenant.Deleted)
            .Select(m => m.TenantId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<CustomerListResponseDto> GetCustomers(CustomerQueryDto query)
    {
        var tenantId = RequireActiveTenantId();
        var customers = CustomerQuery(tenantId, query.Search);

        if (!string.IsNullOrWhiteSpace(query.CityFilter))
        {
            var city = query.CityFilter.Trim();
            customers = customers.Where(c => c.City != null && c.City.Contains(city));
        }

        if (!string.IsNullOrWhiteSpace(query.StateFilter))
        {
            var state = query.StateFilter.Trim();
            customers = customers.Where(c => c.State != null && c.State.Contains(state));
        }

        customers = ApplyCustomerSort(customers, query.SortBy, query.SortDescending);

        var total = await customers.CountAsync();
        var take = NormalizeTake(query.Take);
        var items = await customers
            .Skip(Math.Max(query.Skip, 0))
            .Take(take)
            .Select(c => TransportadosMaps.ToCustomerDto(c))
            .ToListAsync();

        return new CustomerListResponseDto { Total = total, Customers = items };
    }

    public async Task<List<CustomerDto>> GetCustomerList(string? search, int take = 200)
    {
        var tenantId = RequireActiveTenantId();
        return await CustomerQuery(tenantId, search)
            .OrderBy(c => c.FullName)
            .Take(Math.Clamp(take, 1, 500))
            .Select(c => TransportadosMaps.ToCustomerDto(c))
            .ToListAsync();
    }

    public async Task<CustomerDto?> GetCustomer(Guid id)
    {
        var tenantId = RequireActiveTenantId();
        var customer = await dbContext.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.Deleted);

        return customer == null ? null : TransportadosMaps.ToCustomerDto(customer);
    }

    public async Task<CustomerDto> SaveCustomer(CustomerDto request)
    {
        var tenantId = RequireActiveTenantId();
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new InvalidOperationException("Debe indicar el nombre del cliente.");
        }

        Customer customer;
        if (request.Id == Guid.Empty)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId
            };
            dbContext.Customers.Add(customer);
        }
        else
        {
            customer = await dbContext.Customers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == request.Id && c.TenantId == tenantId && !c.Deleted)
                ?? throw new EntityNotFoundException("Cliente no encontrado.");
        }

        ApplyCustomerValues(
            customer,
            request.Code,
            request.FullName,
            request.Latitud,
            request.Longitud,
            request.Email,
            request.AddressLine1,
            request.AddressLine2,
            request.Notes,
            request.City,
            request.State,
            request.Phone);

        customer.FullName = request.FullName.Trim();
        await dbContext.SaveChangesAsync();
        return TransportadosMaps.ToCustomerDto(customer);
    }

    public async Task DeleteCustomer(Guid id)
    {
        var tenantId = RequireActiveTenantId();
        var customer = await dbContext.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantId && !c.Deleted)
            ?? throw new EntityNotFoundException("Cliente no encontrado.");

        customer.Deleted = true;
        await dbContext.SaveChangesAsync();
    }

    public async Task<UserListResponseDto> GetUsers(UserQueryDto query)
    {
        var tenantId = RequireActiveTenantId();
        var users = TenantUsersQuery(tenantId, query.Search);
        var total = await users.CountAsync();
        var items = await users
            .OrderBy(u => u.FullName)
            .Skip(Math.Max(query.Skip, 0))
            .Take(NormalizeTake(query.Take))
            .ToListAsync();

        return new UserListResponseDto
        {
            Total = total,
            Users = await MapUsers(items)
        };
    }

    public async Task<List<UserDto>> GetUserList()
    {
        var tenantId = RequireActiveTenantId();
        return await MapUsers(await TenantUsersQuery(tenantId, null).OrderBy(u => u.FullName).ToListAsync());
    }

    public async Task<UserDto?> GetUser(Guid id)
    {
        var tenantId = RequireActiveTenantId();
        var user = await TenantUsersQuery(tenantId, null).FirstOrDefaultAsync(u => u.Id == id);
        return user == null ? null : (await MapUsers([user])).First();
    }

    public async Task<UserDto?> GetUserByEmail(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && !u.Deleted);

        return user == null ? null : (await MapUsers([user])).First();
    }

    public async Task<UserDto> SaveUser(UserSaveRequestDto request)
    {
        var tenantId = request.TenantId ?? RequireActiveTenantId();
        EnsureCanAccessTenant(tenantId);
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new InvalidOperationException("Debe indicar el nombre del usuario.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !IsValidEmail(request.Email))
        {
            throw new InvalidOperationException("Debe indicar un email valido.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var role = NormalizeRole(request.Role) ?? Roles.Tech;
        if (!Roles.IsTenantRole(role))
        {
            throw new InvalidOperationException("Rol invalido.");
        }

        User user;
        if (request.Id == Guid.Empty)
        {
            if (await dbContext.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == normalizedEmail && !u.Deleted))
            {
                throw new InvalidOperationException("Ya existe un usuario con ese email.");
            }

            user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Users.Add(user);
        }
        else
        {
            user = await dbContext.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == request.Id && !u.Deleted)
                ?? throw new EntityNotFoundException("Usuario no encontrado.");
        }

        user.FullName = request.FullName.Trim();
        user.Email = normalizedEmail;
        user.Photo = NullIfEmpty(request.Photo);
        user.DefaultRole = role;
        user.LastActiveTenantId = tenantId;

        var member = await dbContext.TenantMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == user.Id && m.TenantId == tenantId);
        if (member == null)
        {
            member = new TenantMember
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id
            };
            dbContext.TenantMembers.Add(member);
        }

        member.Role = role;
        member.Deleted = false;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            await UpsertPassword(user.Id, request.Password);
        }

        await dbContext.SaveChangesAsync();
        return (await MapUsers([user])).First();
    }

    public async Task DeleteUser(Guid id)
    {
        var tenantId = RequireActiveTenantId();
        var member = await dbContext.TenantMembers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == id && m.TenantId == tenantId && !m.Deleted)
            ?? throw new EntityNotFoundException("Usuario no encontrado.");

        member.Deleted = true;
        var remainingMemberships = await dbContext.TenantMembers
            .IgnoreQueryFilters()
            .AnyAsync(m => m.UserId == id && m.Id != member.Id && !m.Deleted);
        if (!remainingMemberships)
        {
            var user = await dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
            if (user != null)
            {
                user.Deleted = true;
            }
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task<SettingsDto> GetSettings()
    {
        var tenantId = RequireActiveTenantId();
        return TransportadosMaps.ToSettingsDto(await GetSettingsEntity(tenantId));
    }

    public async Task<SettingsDto> SaveSettings(SettingsDto request)
    {
        var tenantId = RequireActiveTenantId();
        var settings = await GetSettingsEntity(tenantId);
        settings.Name = NullIfEmpty(request.Name);
        settings.NameOfficeOne = NullIfEmpty(request.NameOfficeOne);
        settings.AddressOfficeOne = NullIfEmpty(request.AddressOfficeOne);
        settings.ContactOfficeOne = NullIfEmpty(request.ContactOfficeOne);
        settings.NameOfficeTwo = NullIfEmpty(request.NameOfficeTwo);
        settings.AddressOfficeTwo = NullIfEmpty(request.AddressOfficeTwo);
        settings.ContactOfficeTwo = NullIfEmpty(request.ContactOfficeTwo);
        settings.Highlighted = NullIfEmpty(request.Highlighted);
        settings.LogoImage = NullIfEmpty(request.LogoImage);
        settings.LogoMenu = NullIfEmpty(request.LogoMenu);
        settings.AppTheme = NullIfEmpty(request.AppTheme);
        settings.TechRoleLabel = string.IsNullOrWhiteSpace(request.TechRoleLabel)
            ? Roles.DefaultTechRoleLabel
            : request.TechRoleLabel.Trim();
        settings.SmtpPort = request.SmtpPort;
        settings.SmtpHost = NullIfEmpty(request.SmtpHost);
        settings.SmtpUser = NullIfEmpty(request.SmtpUser);
        settings.SmtpPass = NullIfEmpty(request.SmtpPass);
        settings.SmtpUseSSL = request.SmtpUseSSL;
        settings.EmailFrom = NullIfEmpty(request.EmailFrom);
        settings.SendWOCopyTo = NullIfEmpty(request.SendWOCopyTo);
        var tenant = await dbContext.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId && !t.Deleted);
        if (tenant != null)
        {
            tenant.Name = string.IsNullOrWhiteSpace(settings.Name) ? tenant.Name : settings.Name.Trim();
            tenant.LogoImage = settings.LogoImage;
            tenant.LogoMenu = settings.LogoMenu;
            tenant.ThemeConfig = settings.AppTheme;
        }

        await dbContext.SaveChangesAsync();
        return TransportadosMaps.ToSettingsDto(settings);
    }

    public async Task<TenantFeatureFlagsDto> GetActiveTenantFeatureFlags() =>
        await BuildFeatureFlags(RequireActiveTenantId());

    public async Task<List<TenantListItemDto>> GetTenantList(int page, int pageSize)
    {
        RequireSuperAdmin();
        var skip = Math.Max(page - 1, 0) * Math.Clamp(pageSize, 1, 100);
        var take = Math.Clamp(pageSize, 1, 100);
        var tenants = await dbContext.Tenants
            .IgnoreQueryFilters()
            .Where(t => !t.Deleted)
            .OrderBy(t => t.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        var result = new List<TenantListItemDto>();
        foreach (var tenant in tenants)
        {
            result.Add(await MapTenantListItem(tenant));
        }

        return result;
    }

    public async Task<TenantDetailDto?> GetTenantById(Guid id)
    {
        RequireSuperAdmin();
        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id && !t.Deleted);
        if (tenant == null)
        {
            return null;
        }

        var dto = new TenantDetailDto
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            Status = tenant.Status,
            IsDemo = tenant.IsDemo,
            RegistrationContactFullName = tenant.RegistrationContactFullName,
            RegistrationContactEmail = tenant.RegistrationContactEmail,
            RegistrationContactPhone = tenant.RegistrationContactPhone,
            Features = TransportadosMaps.ToTenantFeatureFlagsDto(tenant)
        };
        return dto;
    }

    public async Task<bool> UpdateTenant(Guid id, TenantUpdateRequestDto request, string? publicOrigin = null)
    {
        RequireSuperAdmin();
        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id && !t.Deleted);
        if (tenant == null)
        {
            return false;
        }

        var previousStatus = tenant.Status;
        if (request.Status.HasValue)
        {
            tenant.Status = request.Status.Value;
        }

        if (request.Features != null)
        {
            tenant.FeatureAppearanceEnabled = request.Features.Appearance;
            tenant.FeatureEmailEnabled = request.Features.Email;
        }

        await dbContext.SaveChangesAsync();
        if (previousStatus != TenantStatus.Active && tenant.Status == TenantStatus.Active)
        {
            await NotifyCompanyActivationAsync(tenant, publicOrigin);
        }

        return true;
    }

    public async Task<bool> DeleteTenant(Guid id)
    {
        RequireSuperAdmin();
        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id && !t.Deleted);
        if (tenant == null)
        {
            return false;
        }

        tenant.Deleted = true;
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> PhysicallyDeleteTenant(Guid id, TenantPhysicalDeleteRequestDto request)
    {
        RequireSuperAdmin();
        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tenant == null)
        {
            return false;
        }

        if (!string.Equals(tenant.Name, request.ConfirmationTenantName?.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("El nombre de confirmacion no coincide con el tenant.");
        }

        var members = await dbContext.TenantMembers.IgnoreQueryFilters().Where(m => m.TenantId == id).ToListAsync();
        var userIds = members.Select(m => m.UserId).Distinct().ToList();
        var customers = await dbContext.Customers.IgnoreQueryFilters().Where(c => c.TenantId == id).ToListAsync();
        var settings = await dbContext.Settings.IgnoreQueryFilters().Where(s => s.TenantId == id).ToListAsync();
        var demoTenantCredentials = await dbContext.DemoTenantCredentials.IgnoreQueryFilters().Where(c => c.TenantId == id).ToListAsync();

        dbContext.Customers.RemoveRange(customers);
        dbContext.Settings.RemoveRange(settings);
        dbContext.DemoTenantCredentials.RemoveRange(demoTenantCredentials);
        dbContext.TenantMembers.RemoveRange(members);
        dbContext.Tenants.Remove(tenant);

        foreach (var userId in userIds)
        {
            var hasOtherMembership = await dbContext.TenantMembers
                .IgnoreQueryFilters()
                .AnyAsync(m => m.UserId == userId && m.TenantId != id);
            if (hasOtherMembership)
            {
                continue;
            }

            var demoUsers = await dbContext.DemoUsers.IgnoreQueryFilters().Where(d => d.UserId == userId).ToListAsync();
            var passwords = await dbContext.UserPasswords.IgnoreQueryFilters().Where(p => p.UserId == userId).ToListAsync();
            var user = await dbContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId && !u.IsSuperAdmin);
            dbContext.DemoUsers.RemoveRange(demoUsers);
            dbContext.UserPasswords.RemoveRange(passwords);
            if (user != null)
            {
                dbContext.Users.Remove(user);
            }
        }

        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<PlatformStatsDto> GetPlatformStats()
    {
        RequireSuperAdmin();
        return new PlatformStatsDto
        {
            TotalTenants = await dbContext.Tenants.IgnoreQueryFilters().CountAsync(t => !t.Deleted),
            PendingTenants = await dbContext.Tenants.IgnoreQueryFilters().CountAsync(t => !t.Deleted && t.Status == TenantStatus.Pending),
            ActiveTenants = await dbContext.Tenants.IgnoreQueryFilters().CountAsync(t => !t.Deleted && t.Status == TenantStatus.Active),
            DisabledTenants = await dbContext.Tenants.IgnoreQueryFilters().CountAsync(t => !t.Deleted && t.Status == TenantStatus.Disabled),
            TotalUsers = await dbContext.Users.IgnoreQueryFilters().CountAsync(u => !u.Deleted),
            TotalCustomers = await dbContext.Customers.IgnoreQueryFilters().CountAsync(c => !c.Deleted)
        };
    }

    private IQueryable<Customer> CustomerQuery(Guid tenantId, string? search)
    {
        var query = dbContext.Customers
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && !c.Deleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(c =>
                c.FullName.Contains(value) ||
                (c.Code != null && c.Code.Contains(value)) ||
                (c.Email != null && c.Email.Contains(value)) ||
                (c.Phone != null && c.Phone.Contains(value)) ||
                (c.City != null && c.City.Contains(value)));
        }

        return query;
    }

    private static IQueryable<Customer> ApplyCustomerSort(IQueryable<Customer> query, string? sortBy, bool descending) =>
        (sortBy, descending) switch
        {
            (CustomerSortFields.Code, true) => query.OrderByDescending(c => c.Code).ThenBy(c => c.FullName),
            (CustomerSortFields.Code, false) => query.OrderBy(c => c.Code).ThenBy(c => c.FullName),
            (CustomerSortFields.Email, true) => query.OrderByDescending(c => c.Email).ThenBy(c => c.FullName),
            (CustomerSortFields.Email, false) => query.OrderBy(c => c.Email).ThenBy(c => c.FullName),
            (CustomerSortFields.Phone, true) => query.OrderByDescending(c => c.Phone).ThenBy(c => c.FullName),
            (CustomerSortFields.Phone, false) => query.OrderBy(c => c.Phone).ThenBy(c => c.FullName),
            (CustomerSortFields.City, true) => query.OrderByDescending(c => c.City).ThenBy(c => c.FullName),
            (CustomerSortFields.City, false) => query.OrderBy(c => c.City).ThenBy(c => c.FullName),
            (_, true) => query.OrderByDescending(c => c.FullName),
            _ => query.OrderBy(c => c.FullName)
        };

    private IQueryable<User> TenantUsersQuery(Guid tenantId, string? search)
    {
        var userIds = dbContext.TenantMembers
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId && !m.Deleted)
            .Select(m => m.UserId);
        var query = dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => !u.Deleted && userIds.Contains(u.Id));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(u => u.FullName.Contains(value) || u.Email.Contains(value));
        }

        return query;
    }

    private async Task<List<UserDto>> MapUsers(List<User> users)
    {
        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var memberships = (await GetTenantMembersForUser(user.Id))
                .Select(TransportadosMaps.ToTenantMemberInfoDto)
                .ToList();
            result.Add(TransportadosMaps.ToUserDto(user, memberships));
        }

        return result;
    }

    private async Task<TenantListItemDto> MapTenantListItem(Tenant tenant) =>
        new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            Status = tenant.Status,
            IsDemo = tenant.IsDemo,
            RegistrationContactFullName = tenant.RegistrationContactFullName,
            RegistrationContactEmail = tenant.RegistrationContactEmail,
            RegistrationContactPhone = tenant.RegistrationContactPhone
        };

    private async Task<Settings> GetSettingsEntity(Guid tenantId)
    {
        EnsureCanAccessTenant(tenantId);
        var settings = await dbContext.Settings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.Deleted);
        if (settings != null)
        {
            return settings;
        }

        var tenant = await dbContext.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId);
        settings = new Settings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = tenant?.Name,
            TechRoleLabel = Roles.DefaultTechRoleLabel
        };
        dbContext.Settings.Add(settings);
        await dbContext.SaveChangesAsync();
        return settings;
    }

    private static Settings NewEmptySettings() =>
        new()
        {
            Id = Guid.Empty,
            TenantId = Guid.Empty,
            Name = "Transportados",
            TechRoleLabel = Roles.DefaultTechRoleLabel
        };

    private async Task<TenantFeatureFlagsDto> BuildFeatureFlags(Guid tenantId)
    {
        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId && !t.Deleted);
        return tenant == null ? new TenantFeatureFlagsDto() : TransportadosMaps.ToTenantFeatureFlagsDto(tenant);
    }

    private async Task<string> ResolveTechRoleLabel(Guid tenantId)
    {
        var settings = await dbContext.Settings
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && !s.Deleted)
            .Select(s => s.TechRoleLabel)
            .FirstOrDefaultAsync();
        return string.IsNullOrWhiteSpace(settings) ? Roles.DefaultTechRoleLabel : settings.Trim();
    }

    private async Task UpsertPassword(Guid userId, string password)
    {
        var userPassword = await dbContext.UserPasswords
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == userId);
        if (userPassword == null)
        {
            dbContext.UserPasswords.Add(new UserPassword
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                HashedPassword = passwordHashingManager.HashToString(password)
            });
            return;
        }

        userPassword.Deleted = false;
        userPassword.HashedPassword = passwordHashingManager.HashToString(password);
    }

    private async Task PersistLastContextAsync(User user, string? role, Guid? tenantId)
    {
        user.DefaultRole = role;
        user.LastActiveTenantId = tenantId;
        await dbContext.SaveChangesAsync();
    }

    private Guid RequireActiveTenantId()
    {
        var tenantId = LoggedUser?.ActiveTenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            throw new ContextAccessException(ApiErrorCodes.ContextInvalid, "Se requiere un tenant activo.");
        }

        EnsureCanAccessTenant(tenantId.Value);
        return tenantId.Value;
    }

    private void EnsureCanAccessTenant(Guid tenantId)
    {
        if (LoggedUser == null)
        {
            throw new AuthenticationException("Authenticated user context could not be resolved.");
        }

        if (LoggedUser.IsSuperAdmin)
        {
            return;
        }

        var allowed = LoggedUser.AllowedTenantIds.Contains(tenantId) ||
            LoggedUser.TenantMemberships.Any(m => m.TenantId == tenantId);
        if (!allowed)
        {
            throw new ContextAccessException(ApiErrorCodes.TenantAccessDenied, "El usuario no puede acceder a este tenant.");
        }
    }

    private void RequireSuperAdmin()
    {
        if (LoggedUser?.IsSuperAdmin != true)
        {
            throw new ContextAccessException(ApiErrorCodes.TenantAccessDenied, "Se requieren permisos de superadmin.");
        }
    }

    private async Task NotifyCompanyRegistrationAsync(Tenant tenant, User admin, string? publicOrigin)
    {
        if (_emailSender?.IsEnabled != true)
        {
            return;
        }

        var transport = _emailTransportOptions.ToTransportSettings(NewEmptySettings());
        if (!EmailTransportOptions.IsTransportConfigured(transport))
        {
            return;
        }

        var body = new StringBuilder()
            .AppendLine("Nueva solicitud de alta de empresa en Transportados.")
            .AppendLine()
            .AppendLine($"Empresa: {tenant.Name}")
            .AppendLine($"Contacto: {tenant.RegistrationContactFullName}")
            .AppendLine($"Email: {tenant.RegistrationContactEmail}")
            .ToString();

        var superAdmins = await dbContext.Users
            .IgnoreQueryFilters()
            .Where(user => user.IsSuperAdmin && !user.Deleted && !string.IsNullOrWhiteSpace(user.Email))
            .ToListAsync();

        foreach (var superAdmin in superAdmins)
        {
            await _emailSender.SendEmailAsync(new EmailSendRequest
            {
                TenantId = tenant.Id,
                TemplateKey = PendingCompanyNotificationTemplateKey,
                IdempotencyKey = CreateIdempotencyKey("transportados-company-registration", tenant.Id.ToString("D"), superAdmin.Email),
                RecipientEmail = superAdmin.Email,
                RecipientDisplayName = superAdmin.FullName,
                Subject = $"Nueva empresa pendiente: {tenant.Name}",
                Body = body,
                Transport = transport
            });
        }
    }

    private async Task NotifyCompanyActivationAsync(Tenant tenant, string? publicOrigin)
    {
        if (_emailSender?.IsEnabled != true || string.IsNullOrWhiteSpace(tenant.RegistrationContactEmail))
        {
            return;
        }

        var transport = _emailTransportOptions.ToTransportSettings(NewEmptySettings());
        if (!EmailTransportOptions.IsTransportConfigured(transport))
        {
            return;
        }

        var loginLink = BuildLoginLink(publicOrigin);
        var body = new StringBuilder()
            .AppendLine($"Tu organizacion {tenant.Name} fue activada.")
            .AppendLine()
            .AppendLine("Ya podes ingresar con el usuario administrador registrado.")
            .AppendLine(string.IsNullOrWhiteSpace(loginLink) ? string.Empty : $"Acceso: {loginLink}")
            .ToString();

        await _emailSender.SendEmailAsync(new EmailSendRequest
        {
            TenantId = tenant.Id,
            TemplateKey = CompanyAdminFirstLoginTemplateKey,
            IdempotencyKey = CreateIdempotencyKey("transportados-company-activated", tenant.Id.ToString("D")),
            RecipientEmail = tenant.RegistrationContactEmail,
            RecipientDisplayName = tenant.RegistrationContactFullName,
            Subject = $"Transportados activo para {tenant.Name}",
            Body = body,
            Transport = transport
        });
    }

    private bool VerifyPassword(string clearText, string hash)
    {
        try
        {
            return passwordHashingManager.Verify(clearText, hash);
        }
        catch
        {
            return false;
        }
    }

    private static int NormalizeTake(int take) => Math.Clamp(take <= 0 ? 20 : take, 1, 500);

    private static Guid? NormalizeTenantId(Guid? tenantId) =>
        tenantId.HasValue && tenantId.Value != Guid.Empty ? tenantId.Value : null;

    private static string? NormalizeRole(string? role) => Roles.Normalize(role);

    private async Task<string> CreateUniqueSlug(string value)
    {
        var baseSlug = Slugify(value);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "tenant";
        }

        var slug = baseSlug;
        var suffix = 1;
        while (await dbContext.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == slug))
        {
            suffix++;
            slug = $"{baseSlug}-{suffix}";
        }

        return slug;
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var lastWasDash = false;
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            if (char.IsLetterOrDigit(lower))
            {
                builder.Append(lower);
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email.Trim());
            return string.Equals(address.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string GenerateSixDigitString() =>
        RandomNumberGenerator.GetInt32(100000, 999999).ToString(CultureInfo.InvariantCulture);

    private static string? BuildPasswordRecoveryLink(string? publicOrigin, string email, string verificationCode)
    {
        var origin = NormalizePublicOrigin(publicOrigin);
        if (origin == null)
        {
            return null;
        }

        return $"{origin}/password-recovery?email={Uri.EscapeDataString(email)}&code={Uri.EscapeDataString(verificationCode)}";
    }

    private static string? BuildLoginLink(string? publicOrigin)
    {
        var origin = NormalizePublicOrigin(publicOrigin);
        return origin == null ? null : $"{origin}/login";
    }

    private static string? NormalizePublicOrigin(string? publicOrigin)
    {
        if (string.IsNullOrWhiteSpace(publicOrigin) ||
            !Uri.TryCreate(publicOrigin.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static string BuildPasswordRecoveryEmailBody(string email, string code, string? directAccessLink)
    {
        var body = new StringBuilder()
            .AppendLine("Solicitaste recuperar tu clave de Transportados.")
            .AppendLine()
            .AppendLine($"Email: {email}")
            .AppendLine($"Codigo: {code}")
            .AppendLine()
            .AppendLine("El codigo vence en 15 minutos.");

        if (!string.IsNullOrWhiteSpace(directAccessLink))
        {
            body.AppendLine().AppendLine($"Acceso directo: {directAccessLink}");
        }

        return body.ToString();
    }

    private static string CreateIdempotencyKey(string prefix, params string[] values)
    {
        var hashInput = string.Join("|", values.Select(value => value.Trim().ToLowerInvariant()));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput))).ToLowerInvariant();
        return $"{prefix}:{hash}";
    }

    private static bool ApplyCustomerValues(
        Customer customer,
        string? code,
        string? fullName,
        string? latitud,
        string? longitud,
        string? email,
        string? addressLine1,
        string? addressLine2,
        string? notes,
        string? city,
        string? state,
        string? phone)
    {
        var normalizedCode = Truncate(NullIfEmpty(code), 100);
        var normalizedName = Truncate(NullIfEmpty(fullName), 200);
        var normalizedLat = Truncate(NullIfEmpty(latitud), 50);
        var normalizedLng = Truncate(NullIfEmpty(longitud), 50);
        var normalizedEmail = Truncate(NullIfEmpty(email), 256);
        var normalizedAddress1 = Truncate(NullIfEmpty(addressLine1), 500);
        var normalizedAddress2 = Truncate(NullIfEmpty(addressLine2), 500);
        var normalizedNotes = Truncate(NullIfEmpty(notes), 2000);
        var normalizedCity = Truncate(NullIfEmpty(city), 100);
        var normalizedState = Truncate(NullIfEmpty(state), 100);
        var normalizedPhone = Truncate(NullIfEmpty(phone), 50);

        var changed =
            !StringEquals(customer.Code, normalizedCode) ||
            !StringEquals(customer.FullName, normalizedName) ||
            !StringEquals(customer.Latitud, normalizedLat) ||
            !StringEquals(customer.Longitud, normalizedLng) ||
            !StringEquals(customer.Email, normalizedEmail) ||
            !StringEquals(customer.AddressLine1, normalizedAddress1) ||
            !StringEquals(customer.AddressLine2, normalizedAddress2) ||
            !StringEquals(customer.Notes, normalizedNotes) ||
            !StringEquals(customer.City, normalizedCity) ||
            !StringEquals(customer.State, normalizedState) ||
            !StringEquals(customer.Phone, normalizedPhone);

        if (!changed)
        {
            return false;
        }

        customer.Code = normalizedCode;
        customer.FullName = normalizedName ?? customer.FullName;
        customer.Latitud = normalizedLat;
        customer.Longitud = normalizedLng;
        customer.Email = normalizedEmail;
        customer.AddressLine1 = normalizedAddress1;
        customer.AddressLine2 = normalizedAddress2;
        customer.Notes = normalizedNotes;
        customer.City = normalizedCity;
        customer.State = normalizedState;
        customer.Phone = normalizedPhone;
        return true;
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool StringEquals(string? left, string? right) =>
        string.Equals(NullIfEmpty(left), NullIfEmpty(right), StringComparison.Ordinal);
}

public static class TransportadosMaps
{
    public static TenantMemberInfoDto ToTenantMemberInfoDto(TenantMember membership) =>
        new()
        {
            TenantMemberId = membership.Id,
            TenantId = membership.TenantId,
            TenantName = membership.Tenant?.Name ?? string.Empty,
            Role = Roles.Normalize(membership.Role) ?? membership.Role
        };

    public static TenantInfoDto ToTenantInfoDto(Tenant tenant) =>
        new()
        {
            Id = tenant.Id,
            Name = tenant.Name,
            Slug = tenant.Slug,
            Status = tenant.Status,
            IsDemo = tenant.IsDemo
        };

    public static CustomerDto ToCustomerDto(Customer customer) =>
        new()
        {
            Id = customer.Id,
            TenantId = customer.TenantId,
            FullName = customer.FullName,
            Email = customer.Email,
            AddressLine1 = customer.AddressLine1,
            AddressLine2 = customer.AddressLine2,
            City = customer.City,
            State = customer.State,
            Phone = customer.Phone,
            Code = customer.Code,
            Latitud = customer.Latitud,
            Longitud = customer.Longitud,
            Notes = customer.Notes
        };

    public static UserDto ToUserDto(User user, List<TenantMemberInfoDto> memberships) =>
        new()
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Photo = user.Photo,
            IsSuperAdmin = user.IsSuperAdmin,
            TenantMemberships = memberships
        };

    public static SettingsDto ToSettingsDto(Settings settings) =>
        new()
        {
            Id = settings.Id,
            TenantId = settings.TenantId,
            Name = settings.Name,
            NameOfficeOne = settings.NameOfficeOne,
            AddressOfficeOne = settings.AddressOfficeOne,
            ContactOfficeOne = settings.ContactOfficeOne,
            NameOfficeTwo = settings.NameOfficeTwo,
            AddressOfficeTwo = settings.AddressOfficeTwo,
            ContactOfficeTwo = settings.ContactOfficeTwo,
            Highlighted = settings.Highlighted,
            LogoImage = settings.LogoImage,
            LogoMenu = settings.LogoMenu,
            AppTheme = settings.AppTheme,
            TechRoleLabel = string.IsNullOrWhiteSpace(settings.TechRoleLabel) ? Roles.DefaultTechRoleLabel : settings.TechRoleLabel.Trim(),
            SmtpPort = settings.SmtpPort,
            SmtpHost = settings.SmtpHost,
            SmtpUser = settings.SmtpUser,
            SmtpPass = settings.SmtpPass,
            SmtpUseSSL = settings.SmtpUseSSL,
            EmailFrom = settings.EmailFrom,
            SendWOCopyTo = settings.SendWOCopyTo,
            IsSmtpEnabled = settings.IsSmtpEnabled
        };

    public static TenantFeatureFlagsDto ToTenantFeatureFlagsDto(Tenant tenant) =>
        new()
        {
            Appearance = tenant.FeatureAppearanceEnabled,
            Email = tenant.FeatureEmailEnabled
        };
}
