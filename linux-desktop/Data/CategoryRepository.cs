using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.Data;

public sealed class CategoryRepository
{
    private readonly DatabaseContext _context;

    public CategoryRepository(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<List<Category>> GetAllAsync()
    {
        var list = new List<Category>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Description, SortOrder FROM Categories ORDER BY SortOrder, Name";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Category
            {
                Id = Guid.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                SortOrder = reader.GetInt32(3)
            });
        }
        return list;
    }

    public async Task<int> GetCountAsync()
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Categories";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public event EventHandler? CategoriesChanged;

    public async Task AddAsync(Category category)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Categories (Id, Name, Description, SortOrder) VALUES ($id, $name, $description, $sortOrder)";
        command.Parameters.AddWithValue("$id", category.Id.ToString());
        command.Parameters.AddWithValue("$name", category.Name);
        command.Parameters.AddWithValue("$description", category.Description ?? string.Empty);
        command.Parameters.AddWithValue("$sortOrder", category.SortOrder);
        await command.ExecuteNonQueryAsync();
        CategoriesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task UpdateAsync(Category category)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Categories SET Name = $name, Description = $description, SortOrder = $sortOrder WHERE Id = $id";
        command.Parameters.AddWithValue("$id", category.Id.ToString());
        command.Parameters.AddWithValue("$name", category.Name);
        command.Parameters.AddWithValue("$description", category.Description ?? string.Empty);
        command.Parameters.AddWithValue("$sortOrder", category.SortOrder);
        await command.ExecuteNonQueryAsync();
        CategoriesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Categories WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync();
        CategoriesChanged?.Invoke(this, EventArgs.Empty);
    }
}
