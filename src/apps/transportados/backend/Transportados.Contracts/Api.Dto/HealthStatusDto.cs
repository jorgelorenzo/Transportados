namespace Transportados.Contracts.Api.Dto;

public sealed record HealthStatusDto(string Service, string Status, DateTimeOffset TimestampUtc);
