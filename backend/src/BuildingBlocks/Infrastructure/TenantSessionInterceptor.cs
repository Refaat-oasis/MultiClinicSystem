using System.Data;
using System.Data.Common;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// Intercepts database connections to propagate ambient ClinicId and IsPlatformAdmin
/// into SQL Server SESSION_CONTEXT for hardware-enforced Row-Level Security (RLS).
/// </summary>
public sealed class TenantSessionInterceptor : DbConnectionInterceptor
{
    private readonly ICurrentClinic _currentClinic;

    public TenantSessionInterceptor(ICurrentClinic currentClinic)
    {
        _currentClinic = currentClinic ?? throw new ArgumentNullException(nameof(currentClinic));
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetSessionContextAsync(connection, cancellationToken).ConfigureAwait(false);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }

    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        SetSessionContextSync(connection);
        base.ConnectionOpened(connection, eventData);
    }

    private async Task SetSessionContextAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            EXEC sp_set_session_context @key=N'ClinicId', @value=@clinicId;
            EXEC sp_set_session_context @key=N'IsPlatformAdmin', @value=@isAdmin;
        ";

        var clinicParam = command.CreateParameter();
        clinicParam.ParameterName = "@clinicId";
        clinicParam.DbType = DbType.Guid;
        clinicParam.Value = _currentClinic.ClinicId.HasValue ? _currentClinic.ClinicId.Value : DBNull.Value;
        command.Parameters.Add(clinicParam);

        var adminParam = command.CreateParameter();
        adminParam.ParameterName = "@isAdmin";
        adminParam.DbType = DbType.Int32;
        adminParam.Value = _currentClinic.IsPlatformAdmin ? 1 : 0;
        command.Parameters.Add(adminParam);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private void SetSessionContextSync(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            EXEC sp_set_session_context @key=N'ClinicId', @value=@clinicId;
            EXEC sp_set_session_context @key=N'IsPlatformAdmin', @value=@isAdmin;
        ";

        var clinicParam = command.CreateParameter();
        clinicParam.ParameterName = "@clinicId";
        clinicParam.DbType = DbType.Guid;
        clinicParam.Value = _currentClinic.ClinicId.HasValue ? _currentClinic.ClinicId.Value : DBNull.Value;
        command.Parameters.Add(clinicParam);

        var adminParam = command.CreateParameter();
        adminParam.ParameterName = "@isAdmin";
        adminParam.DbType = DbType.Int32;
        adminParam.Value = _currentClinic.IsPlatformAdmin ? 1 : 0;
        command.Parameters.Add(adminParam);

        command.ExecuteNonQuery();
    }
}

