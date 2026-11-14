using System.Data;
using System.Data.Common;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Core.Infrastructure;

internal static class ClinicTenantScopeHelper
{
    public static async Task<T> ExecuteForClinicAsync<T>(
        CurrentClinicContext currentClinicContext,
        Guid clinicId,
        Func<Task<T>> action)
    {
        Guid? prev = currentClinicContext.ClinicId;
        bool wasAdmin = currentClinicContext.IsPlatformAdmin;
        try
        {
            currentClinicContext.SetTenant(clinicId);
            return await action();
        }
        finally
        {
            if (wasAdmin)
            {
                currentClinicContext.SetPlatformAdmin();
            }
            else if (prev.HasValue)
            {
                currentClinicContext.SetTenant(prev.Value);
            }
            else
            {
                currentClinicContext.Clear();
            }
        }
    }

    public static async Task<T> WithDapperConnectionAsync<T>(
        CoreDbContext dbContext,
        Guid? clinicId,
        bool isPlatformAdmin,
        Func<DbConnection, DbTransaction?, Task<T>> query)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        bool openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await dbContext.Database.OpenConnectionAsync();
        }

        try
        {
            await using DbCommand cmd = connection.CreateCommand();
            cmd.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            cmd.CommandText = """
                EXEC sp_set_session_context @key = N'ClinicId', @value = @ClinicId, @read_only = 0;
                EXEC sp_set_session_context @key = N'IsPlatformAdmin', @value = @IsAdmin, @read_only = 0;
                """;
            DbParameter pClinic = cmd.CreateParameter();
            pClinic.ParameterName = "@ClinicId";
            pClinic.Value = clinicId.HasValue ? clinicId.Value.ToString() : DBNull.Value;
            cmd.Parameters.Add(pClinic);

            DbParameter pAdmin = cmd.CreateParameter();
            pAdmin.ParameterName = "@IsAdmin";
            pAdmin.Value = isPlatformAdmin ? 1 : 0;
            cmd.Parameters.Add(pAdmin);

            await cmd.ExecuteNonQueryAsync();
            return await query(connection, cmd.Transaction);
        }
        finally
        {
            if (openedHere)
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}
