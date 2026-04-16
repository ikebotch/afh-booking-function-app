using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AFH.Acs.Infrastructure.Extensions;

public static class ModelBuilderSnakeCaseExtensions
{
    public static void UseUpperSnakeCase(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            entity.SetTableName(ToUpperSnakeCase(tableName));
            var storeObject = tableName is null
                ? default
                : StoreObjectIdentifier.Table(tableName, entity.GetSchema());

            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToUpperSnakeCase(
                    storeObject == default
                        ? property.Name
                        : property.GetColumnName(storeObject)));

            foreach (var key in entity.GetKeys())
                key.SetName(ToUpperSnakeCase(key.GetName()));

            foreach (var key in entity.GetForeignKeys())
                key.SetConstraintName(ToUpperSnakeCase(key.GetConstraintName()));

            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(ToUpperSnakeCase(index.GetDatabaseName()));
        }
    }

    private static string? ToUpperSnakeCase(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var chars = new List<char>(input.Length + 8);
        for (var i = 0; i < input.Length; i++)
        {
            var current = input[i];
            if (i > 0 && char.IsUpper(current) && !char.IsUpper(input[i - 1]))
                chars.Add('_');

            chars.Add(char.ToUpperInvariant(current));
        }

        return new string(chars.ToArray());
    }
}
