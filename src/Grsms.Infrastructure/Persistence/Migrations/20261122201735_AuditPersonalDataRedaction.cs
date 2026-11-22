using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 22 Nov 2026 (docs/41, H1): removes personal values already copied into the audit trail. Every field of the personal-record
    /// tables (except links, flags and timestamps: <see cref="AuditSaveChangesInterceptor.PersonalKeep"/>) and an employee's date of
    /// birth become "(personal data)" in AuditLogs.OldValues / NewValues. The audit rows themselves (who, when, which record, which
    /// fields) stay. Data only; nothing to undo.
    /// </summary>
    public partial class AuditPersonalDataRedaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!(migrationBuilder.ActiveProvider ?? "").Contains("MySql", StringComparison.OrdinalIgnoreCase)) return; // JSON functions below are MySQL's

            foreach (var type in AuditSaveChangesInterceptor.PersonalEntities)
                Redact(migrationBuilder, type.Name, type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => IsColumn(p.PropertyType) && !AuditSaveChangesInterceptor.PersonalKeep.Contains(p.Name))
                    .Select(p => p.Name));
            Redact(migrationBuilder, "Employee", ["DateOfBirth"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The removed values cannot (and must not) come back.
        }

        private static void Redact(MigrationBuilder b, string entity, IEnumerable<string> fields)
        {
            var paths = string.Join(", ", fields.Select(f => $"'$.{f}', '{AuditSaveChangesInterceptor.Redacted}'"));
            foreach (var column in new[] { "OldValues", "NewValues" })
                b.Sql($"UPDATE `AuditLogs` SET `{column}` = JSON_REPLACE(`{column}`, {paths}) " +
                      $"WHERE `EntityName` = '{entity}' AND `{column}` IS NOT NULL AND JSON_VALID(`{column}`);");
        }

        /// <summary>Simple values stored in a column (not the record's lists of children).</summary>
        private static bool IsColumn(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(DateOnly) || t == typeof(Guid);
        }
    }
}
