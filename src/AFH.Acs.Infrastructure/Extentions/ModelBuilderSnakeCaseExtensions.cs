using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

namespace AFH.Acs.Recorder.Infrastructure.Extensions;

public static class ModelBuilderSnakeCaseExtensions
{
    public static void UseUpperSnakeCase(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // ============================
            // TABLE NAME
            // ============================
            if (entity.GetTableName() != null)
                entity.SetTableName(ToUpperSnakeCase(entity.GetTableName()!));

            // ============================
            // COLUMN NAMES
            // ============================
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToUpperSnakeCase(property.GetColumnName()!));

            // ============================
            // PRIMARY / ALTERNATE KEYS
            // ============================
            foreach (var key in entity.GetKeys())
            {
                var name = key.GetName();
                if (!string.IsNullOrEmpty(name))
                    key.SetName(ToUpperSnakeCase(name));
            }

            // ============================
            // FOREIGN KEYS
            // ============================
            foreach (var fk in entity.GetForeignKeys())
            {
                var name = fk.GetConstraintName();
                if (!string.IsNullOrEmpty(name))
                    fk.SetConstraintName(ToUpperSnakeCase(name));
            }

            // ============================
            // INDEXES
            // ============================
            foreach (var index in entity.GetIndexes())
            {
                var dbName = index.GetDatabaseName();
                if (!string.IsNullOrEmpty(dbName))
                    index.SetDatabaseName(ToUpperSnakeCase(dbName!));
            }
        }
    }

    // Converts PascalCase / camelCase / kebab-case into UPPER_SNAKE_CASE
    private static string ToUpperSnakeCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Normal snake conversion
        var snake = Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1_$2");

        // Replace hyphens
        snake = snake.Replace("-", "_");

        return snake.ToUpperInvariant();
    }
}
